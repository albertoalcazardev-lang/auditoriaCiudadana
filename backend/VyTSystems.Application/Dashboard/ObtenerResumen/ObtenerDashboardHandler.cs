using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Common.Validation;
using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Dashboard.ObtenerResumen;

/// <summary>
/// Handler CQRS encargado de resolver la ventana temporal en zona horaria America/Mexico_City y consolidar el resumen del dashboard.
/// </summary>
public sealed class ObtenerDashboardHandler(
    IDashboardReadRepository readRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ObtenerDashboardQuery, Result<DashboardResumenDto>>
{
    private const int MaxProximosVencimientos = 5;

    public async Task<Result<DashboardResumenDto>> Handle(
        ObtenerDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (!DashboardPeriodos.EsValido(request.Periodo))
        {
            return Result.ValidationFailure<DashboardResumenDto>([
                new ValidationError(
                    nameof(ObtenerDashboardQuery.Periodo),
                    "El periodo indicado no es válido. Valores permitidos: este-mes, mes-anterior, ultimos-3-meses, este-anio, todo.")
            ]);
        }

        var periodo = request.Periodo!;
        var (ahoraUtc, hoyMexico) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);
        var (rangoActual, rangoAnterior) = CalcularRangos(periodo, hoyMexico);

        var outcome = await readRepository.ObtenerResumenAsync(
            new ConsultaDashboardParametros(hoyMexico, rangoActual, rangoAnterior),
            cancellationToken);

        if (outcome.Estado == LecturaDashboardEstado.PersistenciaNoDisponible || outcome.Datos is null)
        {
            return Result.Failure<DashboardResumenDto>(
                Error.Failure(
                    "Persistence.Unavailable",
                    "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."));
        }

        var datos = outcome.Datos;
        var porArea = datos.PorArea ?? [];
        var proximosVencimientos = datos.ProximosVencimientos ?? [];
        var actividadReciente = datos.ActividadReciente ?? [];

        ValidarInvariantesIntegridad(datos, hoyMexico, porArea, proximosVencimientos);

        var variacion = CalcularVariacionTotalPorcentaje(periodo, datos.Total, datos.TotalPeriodoAnterior);

        var resumen = new DashboardResumenDto(
            Periodo: periodo,
            FechaReferencia: hoyMexico,
            FechaInicio: rangoActual?.FechaInicio,
            FechaFin: rangoActual?.FechaFin,
            ActualizadoEnUtc: ahoraUtc,
            Disponibilidad: DashboardDisponibilidadDto.Completa,
            Indicadores: new DashboardIndicadoresDto(
                Total: datos.Total,
                EnProceso: datos.EnProceso,
                Entregadas: datos.Entregadas,
                Vencidas: datos.Vencidas,
                ProximasAVencer: datos.ProximasAVencer,
                VariacionTotalPorcentaje: variacion),
            PorArea: porArea,
            ProximosVencimientos: proximosVencimientos,
            ActividadReciente: actividadReciente);

        return Result.Success(resumen);
    }

    private static void ValidarInvariantesIntegridad(
        DatosLecturaDashboard datos,
        DateOnly hoyMexico,
        IReadOnlyList<DashboardAreaResumenDto> porArea,
        IReadOnlyList<DashboardProximoVencimientoDto> proximosVencimientos)
    {
        if (datos.Total < 0 ||
            datos.EnProceso < 0 ||
            datos.Entregadas < 0 ||
            datos.Vencidas < 0 ||
            datos.ProximasAVencer < 0 ||
            datos.TotalPeriodoAnterior is < 0)
        {
            throw new InvalidOperationException(
                "Inconsistencia en datos del dashboard: ningún conteo de solicitudes puede ser negativo.");
        }

        if ((long)datos.EnProceso + datos.Entregadas + datos.Vencidas != datos.Total)
        {
            throw new InvalidOperationException(
                "Inconsistencia de reconciliación en el dashboard: Total debe ser igual a EnProceso + Entregadas + Vencidas.");
        }

        if (datos.ProximasAVencer > datos.EnProceso)
        {
            throw new InvalidOperationException(
                "Inconsistencia en indicadores del dashboard: ProximasAVencer no puede exceder EnProceso.");
        }

        if (proximosVencimientos.Count > Math.Min(MaxProximosVencimientos, datos.ProximasAVencer))
        {
            throw new InvalidOperationException(
                "Inconsistencia en próximos vencimientos del dashboard: la cantidad de elementos excede el límite permitido o ProximasAVencer.");
        }

        foreach (var vencimiento in proximosVencimientos)
        {
            if (vencimiento is null ||
                vencimiento.DiasRestantes is < 1 or > 7 ||
                vencimiento.FechaVencimiento != hoyMexico.AddDays(vencimiento.DiasRestantes))
            {
                throw new InvalidOperationException(
                    "Inconsistencia en próximos vencimientos del dashboard: cada elemento debe vencer entre 1 y 7 días a partir de la fecha de referencia.");
            }
        }

        long sumaPorArea = 0;
        foreach (var itemArea in porArea)
        {
            if (itemArea is null || string.IsNullOrWhiteSpace(itemArea.Area) || itemArea.Cantidad < 0)
            {
                throw new InvalidOperationException(
                    "Inconsistencia en agrupación por área del dashboard: cada área debe tener nombre válido y cantidad no negativa.");
            }

            sumaPorArea += itemArea.Cantidad;
        }

        if (sumaPorArea != datos.Total)
        {
            throw new InvalidOperationException(
                "Inconsistencia de reconciliación en el dashboard: la suma de cantidades por área debe coincidir exactamente con Total.");
        }
    }

    private static (RangoFechasDashboard? RangoActual, RangoFechasDashboard? RangoAnterior) CalcularRangos(
        string periodo,
        DateOnly hoyMexico)
    {
        var inicioMes = new DateOnly(hoyMexico.Year, hoyMexico.Month, 1);
        var finMes = inicioMes.AddMonths(1).AddDays(-1);

        return periodo switch
        {
            DashboardPeriodos.EsteMes => (
                new RangoFechasDashboard(inicioMes, finMes),
                new RangoFechasDashboard(inicioMes.AddMonths(-1), inicioMes.AddDays(-1))),

            DashboardPeriodos.MesAnterior => (
                new RangoFechasDashboard(inicioMes.AddMonths(-1), inicioMes.AddDays(-1)),
                new RangoFechasDashboard(inicioMes.AddMonths(-2), inicioMes.AddMonths(-1).AddDays(-1))),

            DashboardPeriodos.Ultimos3Meses => (
                new RangoFechasDashboard(inicioMes.AddMonths(-2), finMes),
                new RangoFechasDashboard(inicioMes.AddMonths(-5), inicioMes.AddMonths(-2).AddDays(-1))),

            DashboardPeriodos.EsteAnio => (
                new RangoFechasDashboard(new DateOnly(hoyMexico.Year, 1, 1), new DateOnly(hoyMexico.Year, 12, 31)),
                hoyMexico.Year > 1
                    ? new RangoFechasDashboard(
                        new DateOnly(hoyMexico.Year - 1, 1, 1),
                        new DateOnly(hoyMexico.Year - 1, 12, 31))
                    : null),

            DashboardPeriodos.Todo => (null, null),

            _ => throw new ArgumentOutOfRangeException(nameof(periodo), "Periodo de dashboard no soportado.")
        };
    }

    private static decimal? CalcularVariacionTotalPorcentaje(
        string periodo,
        int totalActual,
        int? totalPeriodoAnterior)
    {
        if (string.Equals(periodo, DashboardPeriodos.Todo, StringComparison.Ordinal) ||
            totalPeriodoAnterior is null or <= 0)
        {
            return null;
        }

        return Math.Round(
            ((decimal)(totalActual - totalPeriodoAnterior.Value) / totalPeriodoAnterior.Value) * 100m,
            1,
            MidpointRounding.AwayFromZero);
    }
}
