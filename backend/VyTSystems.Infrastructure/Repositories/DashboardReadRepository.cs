using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Repositorio de solo lectura proyectado sobre MySQL 8.4 bajo aislamiento RepeatableRead para garantizar
/// consistencia total entre conteos ejecutivos, agrupación por área y listados resumidos del dashboard.
/// </summary>
internal sealed class DashboardReadRepository(
    VyTSystemsDbContext dbContext,
    ILogger<DashboardReadRepository> logger) : IDashboardReadRepository
{
    private const int MaxAreasPrincipales = 6;
    private const int MaxElementosResumen = 5;

    public async Task<LecturaDashboardOutcome> ObtenerResumenAsync(
        ConsultaDashboardParametros parametros,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);

            var hoy = parametros.HoyMexico;
            var manana = hoy.AddDays(1);
            var hoyMas7 = hoy.AddDays(7);

            IQueryable<Solicitud> solicitudesPeriodoQuery = dbContext.Solicitudes.AsNoTracking();
            if (parametros.RangoActual is { } rangoActual)
            {
                var inicioActual = rangoActual.FechaInicio;
                var finActual = rangoActual.FechaFin;
                solicitudesPeriodoQuery = solicitudesPeriodoQuery
                    .Where(s => s.FechaEntrega >= inicioActual && s.FechaEntrega <= finActual);
            }

            var entregadas = await solicitudesPeriodoQuery
                .CountAsync(s => s.EstatusEntrega == EstatusEntrega.Entregada, cancellationToken);

            var vencidas = await solicitudesPeriodoQuery
                .CountAsync(
                    s => s.EstatusEntrega != EstatusEntrega.Entregada && s.FechaVencimiento < hoy,
                    cancellationToken);

            var enProceso = await solicitudesPeriodoQuery
                .CountAsync(
                    s => s.EstatusEntrega != EstatusEntrega.Entregada && s.FechaVencimiento >= hoy,
                    cancellationToken);

            var total = enProceso + entregadas + vencidas;

            var proximasAVencer = await solicitudesPeriodoQuery
                .CountAsync(
                    s => s.EstatusEntrega != EstatusEntrega.Entregada &&
                         s.FechaVencimiento >= manana &&
                         s.FechaVencimiento <= hoyMas7,
                    cancellationToken);

            int? totalPeriodoAnterior = null;
            if (parametros.RangoAnterior is { } rangoAnterior)
            {
                var inicioAnterior = rangoAnterior.FechaInicio;
                var finAnterior = rangoAnterior.FechaFin;
                totalPeriodoAnterior = await dbContext.Solicitudes
                    .AsNoTracking()
                    .CountAsync(
                        s => s.FechaEntrega >= inicioAnterior && s.FechaEntrega <= finAnterior,
                        cancellationToken);
            }

            var filasPorArea = await solicitudesPeriodoQuery
                .Join(
                    dbContext.AreasReceptoras.AsNoTracking(),
                    s => s.AreaReceptoraId,
                    a => a.Id,
                    (_, a) => new { a.Id, a.Nombre })
                .GroupBy(x => new { x.Id, x.Nombre })
                .Select(g => new
                {
                    AreaId = g.Key.Id,
                    AreaNombre = g.Key.Nombre,
                    Cantidad = g.Count()
                })
                .OrderByDescending(x => x.Cantidad)
                .ThenBy(x => x.AreaNombre)
                .ThenBy(x => x.AreaId)
                .ToListAsync(cancellationToken);

            IReadOnlyList<DashboardAreaResumenDto> porArea = filasPorArea.Count <= MaxAreasPrincipales
                ? filasPorArea
                    .Select(x => new DashboardAreaResumenDto(x.AreaNombre, x.Cantidad))
                    .ToArray()
                :
                [
                    .. filasPorArea
                        .Take(MaxAreasPrincipales)
                        .Select(x => new DashboardAreaResumenDto(x.AreaNombre, x.Cantidad)),
                    new DashboardAreaResumenDto(
                        "Otras",
                        filasPorArea.Skip(MaxAreasPrincipales).Sum(x => x.Cantidad))
                ];

            var filasProximosVencimientos = await solicitudesPeriodoQuery
                .Where(s =>
                    s.EstatusEntrega != EstatusEntrega.Entregada &&
                    s.FechaVencimiento >= manana &&
                    s.FechaVencimiento <= hoyMas7)
                .OrderBy(s => s.FechaVencimiento)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id)
                .Take(MaxElementosResumen)
                .Select(s => new
                {
                    s.Folio,
                    s.Asunto,
                    s.FechaVencimiento
                })
                .ToListAsync(cancellationToken);

            IReadOnlyList<DashboardProximoVencimientoDto> proximosVencimientos = filasProximosVencimientos
                .Select(x => new DashboardProximoVencimientoDto(
                    x.Folio,
                    x.Asunto,
                    x.FechaVencimiento,
                    Math.Max(0, x.FechaVencimiento.DayNumber - hoy.DayNumber)))
                .ToArray();

            var filasActividadReciente = await solicitudesPeriodoQuery
                .OrderByDescending(s => s.CreadoEnUtc)
                .ThenByDescending(s => s.Id)
                .Take(MaxElementosResumen)
                .Select(s => new
                {
                    s.Folio,
                    s.CreadoEnUtc
                })
                .ToListAsync(cancellationToken);

            IReadOnlyList<DashboardActividadRecienteDto> actividadReciente = filasActividadReciente
                .Select(x => new DashboardActividadRecienteDto(
                    "NuevaSolicitud",
                    "Nueva solicitud registrada",
                    x.Folio,
                    DateTime.SpecifyKind(x.CreadoEnUtc, DateTimeKind.Utc)))
                .ToArray();

            await transaction.CommitAsync(cancellationToken);

            return LecturaDashboardOutcome.Completada(
                new DatosLecturaDashboard(
                    Total: total,
                    EnProceso: enProceso,
                    Entregadas: entregadas,
                    Vencidas: vencidas,
                    ProximasAVencer: proximasAVencer,
                    TotalPeriodoAnterior: totalPeriodoAnterior,
                    PorArea: porArea,
                    ProximosVencimientos: proximosVencimientos,
                    ActividadReciente: actividadReciente));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (PersistenceExceptionClassifier.EsFalloDisponibilidadORed(ex))
        {
            var cause = ex.GetBaseException();
            var mysqlNumber = PersistenceExceptionClassifier.ObtenerMySqlException(ex)?.Number;
            logger.LogError(
                "Error de disponibilidad al consultar resumen del dashboard: {Tipo}, código {Codigo}.",
                cause.GetType().Name,
                mysqlNumber);
            return LecturaDashboardOutcome.PersistenciaNoDisponible();
        }
    }
}
