using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Solicitudes.Editar;

/// <summary>
/// Resultado del caso de uso de edición con la proyección actualizada y su nueva versión de concurrencia.
/// </summary>
public sealed record EditarSolicitudResult(
    SolicitudEdicionDto Solicitud,
    string VersionConcurrencia);
