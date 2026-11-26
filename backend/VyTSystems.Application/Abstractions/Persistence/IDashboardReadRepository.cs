namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Rango inclusivo de fechas locales (Ciudad de México) aplicado sobre la fecha de entrega de las solicitudes.
/// </summary>
public sealed record RangoFechasDashboard(DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>
/// Parámetros ya normalizados por la capa Application para ejecutar la consulta consistente del dashboard.
/// </summary>
public sealed record ConsultaDashboardParametros(
    DateOnly HoyMexico,
    RangoFechasDashboard? RangoActual,
    RangoFechasDashboard? RangoAnterior);

/// <summary>
/// Conteo agregado de solicitudes por área receptora dentro del periodo consultado.
/// </summary>
public sealed record DashboardAreaResumenDto(string Area, int Cantidad);

/// <summary>
/// Solicitud no entregada cuyo vencimiento ocurre entre 1 y 7 días respecto a la fecha de referencia local.
/// </summary>
public sealed record DashboardProximoVencimientoDto(
    string Folio,
    string Asunto,
    DateOnly FechaVencimiento,
    int DiasRestantes);

/// <summary>
/// Evento verificable de actividad reciente basado en registros reales de solicitudes.
/// </summary>
public sealed record DashboardActividadRecienteDto(
    string Tipo,
    string Descripcion,
    string Folio,
    DateTime OcurridoEnUtc);

/// <summary>
/// Datos proyectados y reconciliados del dashboard administrativo obtenidos bajo una misma lectura consistente.
/// </summary>
public sealed record DatosLecturaDashboard(
    int Total,
    int EnProceso,
    int Entregadas,
    int Vencidas,
    int ProximasAVencer,
    int? TotalPeriodoAnterior,
    IReadOnlyList<DashboardAreaResumenDto> PorArea,
    IReadOnlyList<DashboardProximoVencimientoDto> ProximosVencimientos,
    IReadOnlyList<DashboardActividadRecienteDto> ActividadReciente);

/// <summary>
/// Estado operativo de la lectura del resumen ejecutivo del dashboard.
/// </summary>
public enum LecturaDashboardEstado
{
    Completada = 1,
    PersistenciaNoDisponible = 2
}

/// <summary>
/// Desenlace tipado de la consulta de solo lectura del dashboard administrativo.
/// </summary>
public sealed record LecturaDashboardOutcome(
    LecturaDashboardEstado Estado,
    DatosLecturaDashboard? Datos = null)
{
    public static LecturaDashboardOutcome Completada(DatosLecturaDashboard datos) =>
        new(LecturaDashboardEstado.Completada, datos);

    public static LecturaDashboardOutcome PersistenciaNoDisponible() =>
        new(LecturaDashboardEstado.PersistenciaNoDisponible);
}

/// <summary>
/// Contrato de solo lectura para obtener las métricas y listados resumidos del dashboard administrativo.
/// </summary>
public interface IDashboardReadRepository
{
    Task<LecturaDashboardOutcome> ObtenerResumenAsync(
        ConsultaDashboardParametros parametros,
        CancellationToken cancellationToken = default);
}
