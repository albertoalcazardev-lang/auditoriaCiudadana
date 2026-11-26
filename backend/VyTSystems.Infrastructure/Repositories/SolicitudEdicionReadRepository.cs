using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Repositorio de solo lectura para precargar una solicitud editable por folio normalizado junto con su versión de concurrencia.
/// </summary>
internal sealed class SolicitudEdicionReadRepository(
    VyTSystemsDbContext dbContext,
    ILogger<SolicitudEdicionReadRepository> logger) : ISolicitudEdicionReadRepository
{
    public async Task<LecturaSolicitudEdicionOutcome> ObtenerPorFolioNormalizadoAsync(
        string folioNormalizado,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var proyectada = await dbContext.Solicitudes
                .AsNoTracking()
                .Where(s => s.FolioNormalizado == folioNormalizado)
                .Select(s => new
                {
                    Solicitud = s,
                    ConcurrencyToken = EF.Property<string>(s, "ConcurrencyToken")
                })
                .FirstOrDefaultAsync(cancellationToken);

            return proyectada is null
                ? LecturaSolicitudEdicionOutcome.NoEncontrada()
                : LecturaSolicitudEdicionOutcome.Encontrada(proyectada.Solicitud, proyectada.ConcurrencyToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            var cause = ex.GetBaseException();
            var mysqlNumber = PersistenceExceptionClassifier.ObtenerMySqlException(ex)?.Number;
            logger.LogError(
                "Error de disponibilidad al consultar solicitud para edición: {Tipo}, código {Codigo}.",
                cause.GetType().Name,
                mysqlNumber);
            return LecturaSolicitudEdicionOutcome.PersistenciaNoDisponible();
        }
    }
}
