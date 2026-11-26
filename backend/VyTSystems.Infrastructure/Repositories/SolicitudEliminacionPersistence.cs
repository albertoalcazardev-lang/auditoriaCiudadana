using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Persistencia atómica para eliminar físicamente solicitudes bajo concurrencia optimista y registrar tombstone de idempotencia.
/// </summary>
internal sealed class SolicitudEliminacionPersistence(
    VyTSystemsDbContext dbContext,
    ILogger<SolicitudEliminacionPersistence> logger) : ISolicitudEliminacionPersistence
{
    public async Task<EliminacionSolicitudOutcome> EjecutarEliminacionAsync(
        EliminacionSolicitudPersistRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solicitud = await dbContext.Solicitudes
                .FirstOrDefaultAsync(s => s.Id == request.SolicitudId, cancellationToken);

            if (solicitud is null)
            {
                return EliminacionSolicitudOutcome.NoEncontrada();
            }

            var entry = dbContext.Entry(solicitud);
            var tokenProperty = entry.Property<string>("ConcurrencyToken");
            var tokenActual = tokenProperty.CurrentValue;

            if (!string.Equals(tokenActual, request.VersionConcurrenciaEsperada, StringComparison.Ordinal))
            {
                dbContext.ChangeTracker.Clear();
                return EliminacionSolicitudOutcome.PrecondicionFallida();
            }

            var creadoPorUsuarioId = solicitud.CreadoPorUsuarioId;
            var idempotencyKey = entry.Property<string>("IdempotencyKey").CurrentValue;
            var idempotencyPayloadHash = entry.Property<string>("IdempotencyPayloadHash").CurrentValue;
            var solicitudIdTexto = solicitud.Id.ToString();

            await using var tx = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                tokenProperty.OriginalValue = request.VersionConcurrenciaEsperada;
                dbContext.Solicitudes.Remove(solicitud);
                await dbContext.SaveChangesAsync(cancellationToken);

                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO solicitud_idempotencias_eliminadas
                        (creado_por_usuario_id, idempotency_key, idempotency_payload_hash, solicitud_id, eliminado_en_utc)
                    VALUES
                        ({creadoPorUsuarioId}, {idempotencyKey}, {idempotencyPayloadHash}, {solicitudIdTexto}, {request.EliminadoEnUtc})
                    ON DUPLICATE KEY UPDATE
                        eliminado_en_utc = eliminado_en_utc;",
                    cancellationToken);

                await tx.CommitAsync(cancellationToken);
                return EliminacionSolicitudOutcome.Eliminada();
            }
            catch (DbUpdateConcurrencyException)
            {
                await RollbackSafeAsync(tx);
                dbContext.ChangeTracker.Clear();

                var sigueExistiendo = await dbContext.Solicitudes
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == request.SolicitudId, cancellationToken);

                return sigueExistiendo
                    ? EliminacionSolicitudOutcome.PrecondicionFallida()
                    : EliminacionSolicitudOutcome.NoEncontrada();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await RollbackSafeAsync(tx);
                dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
            {
                await RollbackSafeAsync(tx);
                dbContext.ChangeTracker.Clear();

                var mysqlEx = PersistenceExceptionClassifier.ObtenerMySqlException(ex);
                if (mysqlEx?.Number == 1451)
                {
                    return EliminacionSolicitudOutcome.ConflictoIntegridad();
                }

                LogFalloPersistencia(ex, mysqlEx);
                return EliminacionSolicitudOutcome.PersistenciaNoDisponible();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            dbContext.ChangeTracker.Clear();
            LogFalloPersistencia(ex, PersistenceExceptionClassifier.ObtenerMySqlException(ex));
            return EliminacionSolicitudOutcome.PersistenciaNoDisponible();
        }
    }

    private async Task RollbackSafeAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            logger.LogDebug("Rollback ignorado tras desconexión de base de datos durante eliminación.");
        }
    }

    private void LogFalloPersistencia(Exception ex, MySqlException? mysqlEx)
    {
        var cause = ex.GetBaseException();
        logger.LogError(
            "Error técnico al eliminar solicitud: {Tipo}, código MySQL {Codigo}.",
            cause.GetType().Name,
            mysqlEx?.Number);
    }
}
