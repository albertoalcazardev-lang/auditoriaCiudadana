namespace VyTSystems.API.Contracts.Solicitudes;

public sealed record SolicitudEdicionResponse(
    string Id,
    string Folio,
    DateOnly FechaEntrega,
    string HoraEntrega,
    string AreaReceptoraId,
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
