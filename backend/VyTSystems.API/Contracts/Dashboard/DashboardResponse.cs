namespace VyTSystems.API.Contracts.Dashboard;

public sealed record DashboardDisponibilidadResponse(
    bool Indicadores,
    bool PorArea,
    bool ProximosVencimientos,
    bool ActividadReciente);

public sealed record DashboardIndicadoresResponse(
    int Total,
    int EnProceso,
    int Entregadas,
    int Vencidas,
    int ProximasAVencer,
    decimal? VariacionTotalPorcentaje);

public sealed record DashboardAreaItemResponse(
    string Area,
    int Cantidad);

public sealed record DashboardProximoVencimientoItemResponse(
    string Folio,
    string Asunto,
    string FechaVencimiento,
    int DiasRestantes);

public sealed record DashboardActividadRecienteItemResponse(
    string Tipo,
    string Descripcion,
    string Folio,
    DateTimeOffset OcurridoEnUtc);

public sealed record DashboardResponse(
    string Periodo,
    string FechaReferencia,
    string? FechaInicio,
    string? FechaFin,
    DateTimeOffset ActualizadoEnUtc,
    DashboardDisponibilidadResponse Disponibilidad,
    DashboardIndicadoresResponse Indicadores,
    IReadOnlyList<DashboardAreaItemResponse> PorArea,
    IReadOnlyList<DashboardProximoVencimientoItemResponse> ProximosVencimientos,
    IReadOnlyList<DashboardActividadRecienteItemResponse> ActividadReciente);
