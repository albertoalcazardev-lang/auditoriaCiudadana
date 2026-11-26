using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Repositorio de solo lectura para consultar las áreas receptoras activas ordenadas.
/// </summary>
internal sealed class AreaReceptoraReadRepository(
    VyTSystemsDbContext dbContext,
    ILogger<AreaReceptoraReadRepository> logger) : IAreaReceptoraReadRepository
{
    public async Task<LecturaAreasReceptorasResult> ListarActivasAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var areas = await dbContext.AreasReceptoras
                .AsNoTracking()
                .Where(a => a.Activa)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Nombre)
                .ToListAsync(cancellationToken);

            return new LecturaAreasReceptorasResult(true, areas);
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
                "Error de disponibilidad al listar áreas receptoras: {Tipo}, código {Codigo}.",
                cause.GetType().Name,
                mysqlNumber);
            return new LecturaAreasReceptorasResult(false, []);
        }
    }
}
