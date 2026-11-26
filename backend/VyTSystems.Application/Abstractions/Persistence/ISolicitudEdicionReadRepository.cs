using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Estado operativo de la búsqueda de una solicitud para su precarga en modo edición.
/// </summary>
public enum LecturaSolicitudEdicionEstado
{
    Encontrada = 1,
    NoEncontrada = 2,
    PersistenciaNoDisponible = 3
}

/// <summary>
/// Desenlace tipado de la consulta por folio normalizado junto con la versión de concurrencia vigente.
/// </summary>
public sealed record LecturaSolicitudEdicionOutcome(
    LecturaSolicitudEdicionEstado Estado,
    Solicitud? Solicitud = null,
    string? VersionConcurrencia = null)
{
    public static LecturaSolicitudEdicionOutcome Encontrada(Solicitud solicitud, string versionConcurrencia) =>
        new(LecturaSolicitudEdicionEstado.Encontrada, solicitud, versionConcurrencia);

    public static LecturaSolicitudEdicionOutcome NoEncontrada() =>
        new(LecturaSolicitudEdicionEstado.NoEncontrada);

    public static LecturaSolicitudEdicionOutcome PersistenciaNoDisponible() =>
        new(LecturaSolicitudEdicionEstado.PersistenciaNoDisponible);
}

/// <summary>
/// Contrato de solo lectura para recuperar una solicitud editable por su folio normalizado.
/// </summary>
public interface ISolicitudEdicionReadRepository
{
    Task<LecturaSolicitudEdicionOutcome> ObtenerPorFolioNormalizadoAsync(
        string folioNormalizado,
        CancellationToken cancellationToken = default);
}
