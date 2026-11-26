using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Repositorio de solo lectura con consultas parametrizadas 100% traducibles a MySQL 8.4 para el listado de solicitudes.
/// </summary>
internal sealed class SolicitudListadoReadRepository(
    VyTSystemsDbContext dbContext,
    ILogger<SolicitudListadoReadRepository> logger) : ISolicitudListadoReadRepository
{
    public async Task<LecturaSolicitudesListadoOutcome> ListarAsync(
        FiltroSolicitudesListado filtro,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<Solicitud> query = dbContext.Solicitudes.AsNoTracking();

            if (!string.IsNullOrEmpty(filtro.FolioParcialNormalizado))
            {
                var patronLike = ConstruirPatronLikeContiene(filtro.FolioParcialNormalizado);
                query = query.Where(s => EF.Functions.Like(s.FolioNormalizado, patronLike, "!"));
            }

            if (filtro.FechaBusquedaExacta is { } fechaExacta)
            {
                query = query.Where(s => s.FechaEntrega == fechaExacta || s.FechaVencimiento == fechaExacta);
            }

            if (filtro.FechaEntregaDesde is { } entDesde)
            {
                query = query.Where(s => s.FechaEntrega >= entDesde);
            }

            if (filtro.FechaEntregaHasta is { } entHasta)
            {
                query = query.Where(s => s.FechaEntrega <= entHasta);
            }

            if (filtro.FechaVencimientoDesde is { } venDesde)
            {
                query = query.Where(s => s.FechaVencimiento >= venDesde);
            }

            if (filtro.FechaVencimientoHasta is { } venHasta)
            {
                query = query.Where(s => s.FechaVencimiento <= venHasta);
            }

            if (filtro.SoloNoEntregadas)
            {
                query = query.Where(s => s.EstatusEntrega != EstatusEntrega.Entregada);
            }

            if (filtro.VencimientoEstrictoMenorQue is { } vencimientoMenorQue)
            {
                query = query.Where(s => s.FechaVencimiento < vencimientoMenorQue);
            }

            if (filtro.DiasVencimientoDesde is { } diasDesde)
            {
                query = query.Where(s => s.FechaVencimiento >= diasDesde);
            }

            if (filtro.DiasVencimientoHasta is { } diasHasta)
            {
                query = query.Where(s => s.FechaVencimiento <= diasHasta);
            }

            var totalItems = await query.CountAsync(cancellationToken);
            var skipLong = (long)(filtro.Page - 1) * filtro.PageSize;

            if (totalItems == 0 || skipLong >= totalItems)
            {
                return LecturaSolicitudesListadoOutcome.Consultado([], totalItems);
            }

            var orderedQuery = AplicarOrdenEstable(query, filtro);

            var items = await orderedQuery
                .Skip((int)skipLong)
                .Take(filtro.PageSize)
                .Select(s => new SolicitudListadoProyeccion(
                    s.Id,
                    s.Folio,
                    s.Asunto,
                    s.AreaReceptora!.Nombre,
                    s.MedioEntrega,
                    s.EstatusEntrega,
                    s.FechaEntrega,
                    s.FechaVencimiento,
                    EF.Property<string>(s, "ConcurrencyToken")))
                .ToListAsync(cancellationToken);

            return LecturaSolicitudesListadoOutcome.Consultado(items, totalItems);
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
                "Error de disponibilidad al consultar listado de solicitudes: {Tipo}, código {Codigo}.",
                cause.GetType().Name,
                mysqlNumber);
            return LecturaSolicitudesListadoOutcome.PersistenciaNoDisponible();
        }
    }

    private static IOrderedQueryable<Solicitud> AplicarOrdenEstable(
        IQueryable<Solicitud> query,
        FiltroSolicitudesListado filtro)
    {
        var hoy = filtro.HoyMexico;

        return (filtro.OrdenCampo, filtro.OrdenDescendente) switch
        {
            (OrdenListadoSolicitudesCampo.Folio, false) => query
                .OrderBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (OrdenListadoSolicitudesCampo.Folio, true) => query
                .OrderByDescending(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (OrdenListadoSolicitudesCampo.Entrega, false) => query
                .OrderBy(s => s.FechaEntrega)
                .ThenBy(s => s.HoraEntrega)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (OrdenListadoSolicitudesCampo.Entrega, true) => query
                .OrderByDescending(s => s.FechaEntrega)
                .ThenByDescending(s => s.HoraEntrega)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (OrdenListadoSolicitudesCampo.Dias, false) => query
                .OrderBy(s => s.EstatusEntrega == EstatusEntrega.Entregada ? 1 : 0)
                .ThenBy(s => s.FechaVencimiento <= hoy ? 0 : 1)
                .ThenBy(s => s.FechaVencimiento)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (OrdenListadoSolicitudesCampo.Dias, true) => query
                .OrderBy(s => s.EstatusEntrega == EstatusEntrega.Entregada ? 1 : 0)
                .ThenByDescending(s => s.FechaVencimiento <= hoy ? 0 : 1)
                .ThenByDescending(s => s.FechaVencimiento)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            (_, true) => query
                .OrderByDescending(s => s.FechaVencimiento)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id),
            _ => query
                .OrderBy(s => s.FechaVencimiento)
                .ThenBy(s => s.FolioNormalizado)
                .ThenBy(s => s.Id)
        };
    }

    private static string ConstruirPatronLikeContiene(string texto)
    {
        var escapado = texto
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);

        return $"%{escapado}%";
    }
}
