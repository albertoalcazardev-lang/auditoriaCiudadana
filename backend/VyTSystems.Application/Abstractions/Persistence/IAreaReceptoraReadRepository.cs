using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Resultado de la lectura de áreas receptoras activas desde el repositorio de consulta.
/// </summary>
public sealed record LecturaAreasReceptorasResult(
    bool Disponible,
    IReadOnlyList<AreaReceptora> Areas);

/// <summary>
/// Contrato de solo lectura para obtener las áreas receptoras activas ordenadas para catálogos.
/// </summary>
public interface IAreaReceptoraReadRepository
{
    Task<LecturaAreasReceptorasResult> ListarActivasAsync(CancellationToken cancellationToken = default);
}
