namespace VyTSystems.Application.Solicitudes.Listar;

/// <summary>
/// Fila proyectada del listado de solicitudes con días restantes y estatus efectivo calculados.
/// </summary>
public sealed record SolicitudListadoItemDto(
    Guid Id,
    string Folio,
    string Asunto,
    string AreaReceptora,
    string MedioEntrega,
    string EstatusEfectivo,
    DateOnly FechaEntrega,
    DateOnly FechaVencimiento,
    int DiasRestantes,
    string VersionConcurrencia);

/// <summary>
/// Resultado paginado del listado administrativo de solicitudes.
/// </summary>
public sealed record ListarSolicitudesResult(
    IReadOnlyList<SolicitudListadoItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
