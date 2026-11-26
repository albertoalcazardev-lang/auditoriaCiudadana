using VyTSystems.Application.Abstractions.Persistence;

namespace VyTSystems.Application.Dashboard.ObtenerResumen;

/// <summary>
/// Banderas explícitas de disponibilidad real por sección del dashboard.
/// </summary>
public sealed record DashboardDisponibilidadDto(
    bool Indicadores,
    bool PorArea,
    bool ProximosVencimientos,
    bool ActividadReciente)
{
    public static readonly DashboardDisponibilidadDto Completa = new(
        Indicadores: true,
        PorArea: true,
        ProximosVencimientos: true,
        ActividadReciente: true);
}

/// <summary>
/// Indicadores ejecutivos reconciliados del periodo consultado.
/// </summary>
public sealed record DashboardIndicadoresDto(
    int Total,
    int EnProceso,
    int Entregadas,
    int Vencidas,
    int ProximasAVencer,
    decimal? VariacionTotalPorcentaje);

/// <summary>
/// Proyección completa del resumen operativo para el Dashboard Administrador.
/// </summary>
public sealed record DashboardResumenDto(
    string Periodo,
    DateOnly FechaReferencia,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    DateTime ActualizadoEnUtc,
    DashboardDisponibilidadDto Disponibilidad,
    DashboardIndicadoresDto Indicadores,
    IReadOnlyList<DashboardAreaResumenDto> PorArea,
    IReadOnlyList<DashboardProximoVencimientoDto> ProximosVencimientos,
    IReadOnlyList<DashboardActividadRecienteDto> ActividadReciente);
