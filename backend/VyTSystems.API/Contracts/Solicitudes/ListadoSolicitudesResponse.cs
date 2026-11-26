namespace VyTSystems.API.Contracts.Solicitudes;

public sealed record SolicitudListadoItemResponse(
    string Id,
    string Folio,
    string Asunto,
    string AreaReceptora,
    string MedioEntrega,
    string EstatusEfectivo,
    DateOnly FechaEntrega,
    DateOnly FechaVencimiento,
    int DiasRestantes,
    string Version);

public sealed record ListadoSolicitudesResponse(
    IReadOnlyList<SolicitudListadoItemResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
