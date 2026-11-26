using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Campos válidos de ordenamiento para el listado administrativo de solicitudes.
/// </summary>
public enum OrdenListadoSolicitudesCampo
{
    Vencimiento = 1,
    Folio = 2,
    Entrega = 3,
    Dias = 4
}

/// <summary>
/// Criterios normalizados y precalculados en C# para consultar solicitudes sin evaluación en memoria.
/// </summary>
public sealed record FiltroSolicitudesListado(
    DateOnly HoyMexico,
    string? FolioParcialNormalizado,
    DateOnly? FechaBusquedaExacta,
    DateOnly? FechaEntregaDesde,
    DateOnly? FechaEntregaHasta,
    DateOnly? FechaVencimientoDesde,
    DateOnly? FechaVencimientoHasta,
    bool SoloNoEntregadas,
    DateOnly? VencimientoEstrictoMenorQue,
    DateOnly? DiasVencimientoDesde,
    DateOnly? DiasVencimientoHasta,
    OrdenListadoSolicitudesCampo OrdenCampo,
    bool OrdenDescendente,
    int Page,
    int PageSize);

/// <summary>
/// Proyección estricta de lectura para filas del listado de solicitudes (sin auditoría interna ni claves de idempotencia).
/// </summary>
public sealed record SolicitudListadoProyeccion(
    Guid Id,
    string Folio,
    string Asunto,
    string AreaReceptoraNombre,
    MedioEntrega MedioEntrega,
    EstatusEntrega EstatusEntrega,
    DateOnly FechaEntrega,
    DateOnly FechaVencimiento,
    string VersionConcurrencia);

/// <summary>
/// Estado operativo de la consulta paginada de solicitudes.
/// </summary>
public enum LecturaSolicitudesListadoEstado
{
    Consultado = 1,
    PersistenciaNoDisponible = 2
}

/// <summary>
/// Desenlace tipado de la consulta paginada del listado de solicitudes.
/// </summary>
public sealed record LecturaSolicitudesListadoOutcome(
    LecturaSolicitudesListadoEstado Estado,
    IReadOnlyList<SolicitudListadoProyeccion> Items,
    int TotalItems)
{
    public static LecturaSolicitudesListadoOutcome Consultado(
        IReadOnlyList<SolicitudListadoProyeccion> items,
        int totalItems) =>
        new(LecturaSolicitudesListadoEstado.Consultado, items, totalItems);

    public static LecturaSolicitudesListadoOutcome PersistenciaNoDisponible() =>
        new(LecturaSolicitudesListadoEstado.PersistenciaNoDisponible, [], 0);
}

/// <summary>
/// Contrato de solo lectura para consultar de forma paginada, filtrada y ordenada el listado de solicitudes.
/// </summary>
public interface ISolicitudListadoReadRepository
{
    Task<LecturaSolicitudesListadoOutcome> ListarAsync(
        FiltroSolicitudesListado filtro,
        CancellationToken cancellationToken = default);
}
