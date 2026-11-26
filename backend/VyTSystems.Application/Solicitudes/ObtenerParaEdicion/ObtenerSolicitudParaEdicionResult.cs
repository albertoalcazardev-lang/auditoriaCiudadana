using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

/// <summary>
/// Resultado de precarga para edición con la proyección de la solicitud y su versión de concurrencia actual.
/// </summary>
public sealed record ObtenerSolicitudParaEdicionResult(
    SolicitudEdicionDto Solicitud,
    string VersionConcurrencia);
