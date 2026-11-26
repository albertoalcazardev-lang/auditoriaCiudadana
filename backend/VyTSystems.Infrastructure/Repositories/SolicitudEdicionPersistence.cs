using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Persistencia atómica de edición de solicitudes con control de concurrencia optimista real sobre MySQL 8.4.
/// </summary>
internal sealed class SolicitudEdicionPersistence(
    VyTSystemsDbContext dbContext,
    ILogger<SolicitudEdicionPersistence> logger) : ISolicitudEdicionPersistence
{
    public async Task<EdicionSolicitudOutcome> EjecutarEdicionAsync(
        EdicionSolicitudPersistRequest request,
        Action<Solicitud> actualizarSolicitud,
        CancellationToken cancellationToken = default)
    {
        try
        {
            try
            {
                var solicitud = await dbContext.Solicitudes
                    .FirstOrDefaultAsync(s => s.Id == request.SolicitudId, cancellationToken);

                if (solicitud is null)
                {
                    return EdicionSolicitudOutcome.NoEncontrada();
                }

                var entry = dbContext.Entry(solicitud);
                var tokenProperty = entry.Property<string>("ConcurrencyToken");
                var tokenActual = tokenProperty.CurrentValue;

                if (!string.Equals(tokenActual, request.VersionConcurrenciaEsperada, StringComparison.Ordinal))
                {
                    dbContext.ChangeTracker.Clear();
                    return EdicionSolicitudOutcome.ConflictoConcurrencia();
                }

                var areaActiva = await dbContext.AreasReceptoras
                    .AsNoTracking()
                    .AnyAsync(a => a.Id == request.AreaReceptoraId && a.Activa, cancellationToken);

                if (!areaActiva)
                {
                    dbContext.ChangeTracker.Clear();
                    return EdicionSolicitudOutcome.AreaReceptoraInvalida();
                }

                tokenProperty.OriginalValue = request.VersionConcurrenciaEsperada;
                actualizarSolicitud(solicitud);

                var nuevaVersion = GenerarNuevaVersion(request.VersionConcurrenciaEsperada);
                tokenProperty.CurrentValue = nuevaVersion;

                await dbContext.SaveChangesAsync(cancellationToken);

                return EdicionSolicitudOutcome.Actualizada(solicitud, nuevaVersion);
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();

                var sigueExistiendo = await dbContext.Solicitudes
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == request.SolicitudId, cancellationToken);

                return sigueExistiendo
                    ? EdicionSolicitudOutcome.ConflictoConcurrencia()
                    : EdicionSolicitudOutcome.NoEncontrada();
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

            var mysqlEx = PersistenceExceptionClassifier.ObtenerMySqlException(ex);
            if (mysqlEx?.Number == 1452)
            {
                try
                {
                    var areaSigueActiva = await dbContext.AreasReceptoras
                        .AsNoTracking()
                        .AnyAsync(a => a.Id == request.AreaReceptoraId && a.Activa, cancellationToken);

                    if (!areaSigueActiva)
                    {
                        return EdicionSolicitudOutcome.AreaReceptoraInvalida();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception innerEx) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(innerEx))
                {
                    LogFalloPersistencia(innerEx, PersistenceExceptionClassifier.ObtenerMySqlException(innerEx));
                    return EdicionSolicitudOutcome.PersistenciaNoDisponible();
                }
            }

            LogFalloPersistencia(ex, mysqlEx);
            return EdicionSolicitudOutcome.PersistenciaNoDisponible();
        }
    }

    private static string GenerarNuevaVersion(string versionEsperada)
    {
        string nuevaVersion;
        do
        {
            nuevaVersion = Guid.NewGuid().ToString("N");
        } while (string.Equals(nuevaVersion, versionEsperada, StringComparison.Ordinal));

        return nuevaVersion;
    }

    private void LogFalloPersistencia(Exception ex, MySqlException? mysqlEx)
    {
        var cause = ex.GetBaseException();
        logger.LogError(
            "Error técnico al actualizar solicitud: {Tipo}, código MySQL {Codigo}.",
            cause.GetType().Name,
            mysqlEx?.Number);
    }
}
