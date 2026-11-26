using System.Globalization;
using Microsoft.Extensions.Primitives;
using VyTSystems.Application.Dashboard.ObtenerResumen;

namespace VyTSystems.API.Contracts.Dashboard;

internal static class DashboardContractMappings
{
    private const string InvalidSentinel = "\u0000";
    private const string IsoDateFormat = "yyyy-MM-dd";

    public static ObtenerDashboardQuery ToQuery(StringValues periodoValues) =>
        periodoValues.Count switch
        {
            0 => new ObtenerDashboardQuery(DashboardPeriodos.EsteMes),
            1 => new ObtenerDashboardQuery(periodoValues[0]),
            _ => new ObtenerDashboardQuery(InvalidSentinel)
        };

    public static DashboardResponse ToResponse(this DashboardResumenDto dto) =>
        new(
            Periodo: dto.Periodo,
            FechaReferencia: dto.FechaReferencia.ToString(IsoDateFormat, CultureInfo.InvariantCulture),
            FechaInicio: dto.FechaInicio?.ToString(IsoDateFormat, CultureInfo.InvariantCulture),
            FechaFin: dto.FechaFin?.ToString(IsoDateFormat, CultureInfo.InvariantCulture),
            ActualizadoEnUtc: ToUtcDateTimeOffset(dto.ActualizadoEnUtc),
            Disponibilidad: new DashboardDisponibilidadResponse(
                Indicadores: dto.Disponibilidad.Indicadores,
                PorArea: dto.Disponibilidad.PorArea,
                ProximosVencimientos: dto.Disponibilidad.ProximosVencimientos,
                ActividadReciente: dto.Disponibilidad.ActividadReciente),
            Indicadores: new DashboardIndicadoresResponse(
                Total: dto.Indicadores.Total,
                EnProceso: dto.Indicadores.EnProceso,
                Entregadas: dto.Indicadores.Entregadas,
                Vencidas: dto.Indicadores.Vencidas,
                ProximasAVencer: dto.Indicadores.ProximasAVencer,
                VariacionTotalPorcentaje: dto.Indicadores.VariacionTotalPorcentaje),
            PorArea: dto.PorArea
                .Select(a => new DashboardAreaItemResponse(a.Area, a.Cantidad))
                .ToArray(),
            ProximosVencimientos: dto.ProximosVencimientos
                .Select(v => new DashboardProximoVencimientoItemResponse(
                    v.Folio,
                    v.Asunto,
                    v.FechaVencimiento.ToString(IsoDateFormat, CultureInfo.InvariantCulture),
                    v.DiasRestantes))
                .ToArray(),
            ActividadReciente: dto.ActividadReciente
                .Select(a => new DashboardActividadRecienteItemResponse(
                    a.Tipo,
                    a.Descripcion,
                    a.Folio,
                    ToUtcDateTimeOffset(a.OcurridoEnUtc)))
                .ToArray());

    private static DateTimeOffset ToUtcDateTimeOffset(DateTime dateTime) =>
        new(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), TimeSpan.Zero);
}
