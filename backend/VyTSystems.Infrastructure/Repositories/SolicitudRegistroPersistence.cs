using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Persistencia transaccional de solicitudes con control de idempotencia y generación atómica de folios sobre MySQL 8.4.
/// </summary>
internal sealed class SolicitudRegistroPersistence(
    VyTSystemsDbContext dbContext,
    ILogger<SolicitudRegistroPersistence> logger) : ISolicitudRegistroPersistence
{
    private const int MaxIntentosTransaccion = 3;

    private sealed record RegistroIdempotente(Solicitud Solicitud, string PayloadHash);

    public async Task<RegistroSolicitudOutcome> EjecutarCreacionAsync(
        RegistroSolicitudPersistRequest request,
        Func<string, Solicitud> construirSolicitud,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var outcomePrevio = await ConsultarOutcomeIdempotenciaAsync(
                request.CreadoPorUsuarioId,
                request.IdempotencyKey,
                request.IdempotencyPayloadHash,
                cancellationToken);

            if (outcomePrevio is not null)
            {
                return outcomePrevio;
            }

            var areaActiva = await dbContext.AreasReceptoras
                .AsNoTracking()
                .AnyAsync(a => a.Id == request.AreaReceptoraId && a.Activa, cancellationToken);

            if (!areaActiva)
            {
                return RegistroSolicitudOutcome.AreaReceptoraInvalida();
            }

            for (var intento = 1; intento <= MaxIntentosTransaccion; intento++)
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                try
                {
                    string folioParaEntidad;

                    if (request.GenerarFolioAutomatico)
                    {
                        await AsegurarFilaSecuenciaAsync(request.AnioFolioAutomatico, cancellationToken);
                        var ultimoNumero = await LeerSecuenciaConBloqueoAsync(
                            request.AnioFolioAutomatico,
                            tx,
                            cancellationToken);

                        // Tras adquirir el lock bajo READ COMMITTED, un reintento o eliminación concurrente ya es visible.
                        var outcomeEnLock = await ConsultarOutcomeIdempotenciaAsync(
                            request.CreadoPorUsuarioId,
                            request.IdempotencyKey,
                            request.IdempotencyPayloadHash,
                            cancellationToken);

                        if (outcomeEnLock is not null)
                        {
                            await RollbackSafeAsync(tx);
                            return outcomeEnLock;
                        }

                        var (folioAsignado, consecutivoAsignado) = await ResolverSiguienteFolioDisponibleAsync(
                            request.AnioFolioAutomatico,
                            ultimoNumero,
                            cancellationToken);

                        await ActualizarSecuenciaAsync(
                            request.AnioFolioAutomatico,
                            consecutivoAsignado,
                            tx,
                            cancellationToken);

                        folioParaEntidad = folioAsignado;
                    }
                    else
                    {
                        var outcomeManual = await ConsultarOutcomeIdempotenciaAsync(
                            request.CreadoPorUsuarioId,
                            request.IdempotencyKey,
                            request.IdempotencyPayloadHash,
                            cancellationToken);

                        if (outcomeManual is not null)
                        {
                            await RollbackSafeAsync(tx);
                            return outcomeManual;
                        }

                        folioParaEntidad = request.FolioManual!;
                    }

                    var solicitud = construirSolicitud(folioParaEntidad);
                    var entry = dbContext.Solicitudes.Add(solicitud);
                    entry.Property("IdempotencyKey").CurrentValue = request.IdempotencyKey;
                    entry.Property("IdempotencyPayloadHash").CurrentValue = request.IdempotencyPayloadHash;
                    entry.Property("ConcurrencyToken").CurrentValue = Guid.NewGuid().ToString("N");

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return RegistroSolicitudOutcome.Creada(solicitud);
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
                    if (mysqlEx?.Number == 1062)
                    {
                        var outcomePostConflicto = await ConsultarOutcomeIdempotenciaAsync(
                            request.CreadoPorUsuarioId,
                            request.IdempotencyKey,
                            request.IdempotencyPayloadHash,
                            cancellationToken);

                        if (outcomePostConflicto is not null)
                        {
                            return outcomePostConflicto;
                        }

                        if (!request.GenerarFolioAutomatico)
                        {
                            return RegistroSolicitudOutcome.FolioDuplicado();
                        }

                        if (intento < MaxIntentosTransaccion)
                        {
                            continue;
                        }
                    }
                    else if (mysqlEx?.Number is 1213 or 1205 && intento < MaxIntentosTransaccion)
                    {
                        continue;
                    }
                    else if (mysqlEx?.Number == 1452)
                    {
                        var areaSigueActiva = await dbContext.AreasReceptoras
                            .AsNoTracking()
                            .AnyAsync(a => a.Id == request.AreaReceptoraId && a.Activa, cancellationToken);

                        if (!areaSigueActiva)
                        {
                            return RegistroSolicitudOutcome.AreaReceptoraInvalida();
                        }
                    }

                    LogFalloPersistencia(ex, mysqlEx);
                    return RegistroSolicitudOutcome.PersistenciaNoDisponible();
                }
            }

            return RegistroSolicitudOutcome.PersistenciaNoDisponible();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            LogFalloPersistencia(ex, PersistenceExceptionClassifier.ObtenerMySqlException(ex));
            return RegistroSolicitudOutcome.PersistenciaNoDisponible();
        }
    }

    private async Task<RegistroSolicitudOutcome?> ConsultarOutcomeIdempotenciaAsync(
        string usuarioId,
        string idempotencyKey,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        // Orden estricto de lectura: 1.º solicitudes, 2.º solicitud_idempotencias_eliminadas
        var existente = await ConsultarPorIdempotenciaAsync(usuarioId, idempotencyKey, cancellationToken);
        if (existente is not null)
        {
            return ResolverResultadoIdempotente(existente, payloadHash);
        }

        var eliminadaPreviamente = await dbContext.SolicitudIdempotenciasEliminadas
            .AsNoTracking()
            .AnyAsync(
                x => x.CreadoPorUsuarioId == usuarioId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        return eliminadaPreviamente
            ? RegistroSolicitudOutcome.IdempotenciaSolicitudEliminada()
            : null;
    }

    private async Task<RegistroIdempotente?> ConsultarPorIdempotenciaAsync(
        string usuarioId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var encontrado = await dbContext.Solicitudes
            .AsNoTracking()
            .Where(s =>
                s.CreadoPorUsuarioId == usuarioId &&
                EF.Property<string>(s, "IdempotencyKey") == idempotencyKey)
            .Select(s => new
            {
                Solicitud = s,
                PayloadHash = EF.Property<string>(s, "IdempotencyPayloadHash")
            })
            .FirstOrDefaultAsync(cancellationToken);

        return encontrado is null
            ? null
            : new RegistroIdempotente(encontrado.Solicitud, encontrado.PayloadHash);
    }

    private static RegistroSolicitudOutcome ResolverResultadoIdempotente(
        RegistroIdempotente existente,
        string payloadHash) =>
        string.Equals(existente.PayloadHash, payloadHash, StringComparison.Ordinal)
            ? RegistroSolicitudOutcome.RecuperadaPorIdempotencia(existente.Solicitud)
            : RegistroSolicitudOutcome.IdempotenciaConflicto();

    private Task AsegurarFilaSecuenciaAsync(int anio, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO folio_secuencias (anio, ultimo_numero) VALUES ({anio}, 0) ON DUPLICATE KEY UPDATE ultimo_numero = ultimo_numero;",
            cancellationToken);

    private async Task<int> LeerSecuenciaConBloqueoAsync(
        int anio,
        IDbContextTransaction tx,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "SELECT ultimo_numero FROM folio_secuencias WHERE anio = @anio FOR UPDATE;";

        var anioParam = command.CreateParameter();
        anioParam.ParameterName = "@anio";
        anioParam.Value = anio;
        command.Parameters.Add(anioParam);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private async Task<(string Folio, int Consecutivo)> ResolverSiguienteFolioDisponibleAsync(
        int anio,
        int ultimoNumero,
        CancellationToken cancellationToken)
    {
        var candidato = ultimoNumero + 1;

        while (true)
        {
            var folio = Solicitud.FormatearFolioAutomatico(anio, candidato);

            var existe = await dbContext.Solicitudes
                .AsNoTracking()
                .AnyAsync(s => s.FolioNormalizado == folio, cancellationToken);

            if (!existe)
            {
                return (folio, candidato);
            }

            candidato++;
        }
    }

    private async Task ActualizarSecuenciaAsync(
        int anio,
        int ultimoNumero,
        IDbContextTransaction tx,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "UPDATE folio_secuencias SET ultimo_numero = @ultimo_numero WHERE anio = @anio;";

        var ultimoParam = command.CreateParameter();
        ultimoParam.ParameterName = "@ultimo_numero";
        ultimoParam.Value = ultimoNumero;
        command.Parameters.Add(ultimoParam);

        var anioParam = command.CreateParameter();
        anioParam.ParameterName = "@anio";
        anioParam.Value = anio;
        command.Parameters.Add(anioParam);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RollbackSafeAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            logger.LogDebug("Rollback ignorado tras desconexión de base de datos.");
        }
    }

    private void LogFalloPersistencia(Exception ex, MySqlException? mysqlEx)
    {
        var cause = ex.GetBaseException();
        logger.LogError(
            "Error técnico al persistir solicitud: {Tipo}, código MySQL {Codigo}.",
            cause.GetType().Name,
            mysqlEx?.Number);
    }
}
