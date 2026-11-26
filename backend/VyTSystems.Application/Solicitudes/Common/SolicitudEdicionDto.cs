namespace VyTSystems.Application.Solicitudes.Common;

/// <summary>
/// Proyección compartida de aplicación para precarga y confirmación de edición de una solicitud.
/// </summary>
public sealed record SolicitudEdicionDto(
    Guid Id,
    string Folio,
    DateOnly FechaEntrega,
    TimeOnly HoraEntrega,
    Guid AreaReceptoraId,
    string Asunto,
    string MedioEntrega,
    string EstatusEntrega,
    string TipoSolicitud,
    string TipoEvidencia,
    bool TieneAcuse,
    string TipoAcuse,
    DateOnly FechaVencimiento,
    int DiasRestantes,
    string EstatusEfectivo);
