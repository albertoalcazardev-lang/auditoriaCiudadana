using System.Globalization;
using MapsterMapper;
using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Listar;

/// <summary>
/// Handler CQRS encargado de orquestar la consulta filtrada, ordenada y paginada del listado de solicitudes.
/// </summary>
public sealed class ListarSolicitudesHandler(
    ISolicitudListadoReadRepository readRepository,
    IMapper mapper,
    TimeProvider timeProvider)
    : IRequestHandler<ListarSolicitudesQuery, Result<ListarSolicitudesResult>>
{
    public async Task<Result<ListarSolicitudesResult>> Handle(
        ListarSolicitudesQuery request,
        CancellationToken cancellationToken)
    {
        var (_, hoyMexico) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);

        var (folioParcialNormalizado, fechaBusquedaExacta) = ResolverBusqueda(request.Q);

        DateOnly? entDesde = SolicitudInputParser.TryParseFecha(request.EntD, out var parsedEntD) ? parsedEntD : null;
        DateOnly? entHasta = SolicitudInputParser.TryParseFecha(request.EntH, out var parsedEntH) ? parsedEntH : null;
        DateOnly? venDesde = SolicitudInputParser.TryParseFecha(request.VenD, out var parsedVenD) ? parsedVenD : null;
        DateOnly? venHasta = SolicitudInputParser.TryParseFecha(request.VenH, out var parsedVenH) ? parsedVenH : null;

        var (soloNoEntregadas, vencimientoMenorQue, diasDesde, diasHasta) = ResolverFiltroDias(
            hoyMexico,
            request.Dias,
            request.DiasMin,
            request.DiasMax);

        var ordenCampo = ResolverOrdenCampo(request.Sort);
        var ordenDescendente = string.Equals(request.Dir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        var page = ListarSolicitudesValidator.TryParseEnteroPositivo(request.Page, out var p) ? p : 1;
        var pageSize = ListarSolicitudesValidator.TryParseTamanoPagina(request.Size, out var s) ? s : 10;

        var filtro = new FiltroSolicitudesListado(
            hoyMexico,
            folioParcialNormalizado,
            fechaBusquedaExacta,
            entDesde,
            entHasta,
            venDesde,
            venHasta,
            soloNoEntregadas,
            vencimientoMenorQue,
            diasDesde,
            diasHasta,
            ordenCampo,
            ordenDescendente,
            page,
            pageSize);

        var outcome = await readRepository.ListarAsync(filtro, cancellationToken);
        if (outcome.Estado == LecturaSolicitudesListadoEstado.PersistenciaNoDisponible)
        {
            return Result.Failure<ListarSolicitudesResult>(
                Error.Failure(
                    "Persistence.Unavailable",
                    "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."));
        }

        var items = outcome.Items
            .Select(item =>
            {
                var dtoBase = mapper.Map<SolicitudListadoItemDto>(item);
                var diasRestantes = Math.Max(0, item.FechaVencimiento.DayNumber - hoyMexico.DayNumber);
                var estatusEfectivo = (item.EstatusEntrega == EstatusEntrega.Entregada
                    ? EstatusEfectivo.Entregada
                    : hoyMexico > item.FechaVencimiento
                        ? EstatusEfectivo.Vencida
                        : EstatusEfectivo.EnProceso).ToString();

                return dtoBase with
                {
                    DiasRestantes = diasRestantes,
                    EstatusEfectivo = estatusEfectivo
                };
            })
            .ToArray();

        var totalPages = outcome.TotalItems == 0
            ? 0
            : (int)Math.Ceiling((double)outcome.TotalItems / pageSize);

        return Result.Success(new ListarSolicitudesResult(
            items,
            page,
            pageSize,
            outcome.TotalItems,
            totalPages));
    }

    private static (string? FolioParcialNormalizado, DateOnly? FechaBusquedaExacta) ResolverBusqueda(string? rawQ)
    {
        if (string.IsNullOrWhiteSpace(rawQ))
        {
            return (null, null);
        }

        var trimmed = rawQ.Trim();
        if (ListarSolicitudesValidator.CandidatoFechaConBarrasRegex().IsMatch(trimmed))
        {
            var fecha = DateOnly.ParseExact(trimmed, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            return (null, fecha);
        }

        var folioNormalizado = Solicitud.EsFolioValido(trimmed)
            ? Solicitud.NormalizarFolio(trimmed)
            : trimmed.ToUpperInvariant();

        return (folioNormalizado, null);
    }

    private static (
        bool SoloNoEntregadas,
        DateOnly? VencimientoEstrictoMenorQue,
        DateOnly? DiasVencimientoDesde,
        DateOnly? DiasVencimientoHasta) ResolverFiltroDias(
        DateOnly hoyMexico,
        string? rawDias,
        string? rawDiasMin,
        string? rawDiasMax)
    {
        if (string.IsNullOrWhiteSpace(rawDias))
        {
            return (false, null, null, null);
        }

        var dias = rawDias.Trim();
        if (string.Equals(dias, "todos", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, null, null);
        }

        if (string.Equals(dias, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dias, "vencidas", StringComparison.OrdinalIgnoreCase))
        {
            return (true, hoyMexico, null, null);
        }

        if (string.Equals(dias, "1-3", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null, SumarDiasSeguro(hoyMexico, 1), SumarDiasSeguro(hoyMexico, 3));
        }

        if (string.Equals(dias, "4-7", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null, SumarDiasSeguro(hoyMexico, 4), SumarDiasSeguro(hoyMexico, 7));
        }

        if (string.Equals(dias, "1-7", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null, SumarDiasSeguro(hoyMexico, 1), SumarDiasSeguro(hoyMexico, 7));
        }

        if (string.Equals(dias, "8+", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null, SumarDiasSeguro(hoyMexico, 8), null);
        }

        var minEf = ListarSolicitudesValidator.TryParseDiasPersonalizados(rawDiasMin, out var minVal) ? minVal : 0;
        var maxEf = ListarSolicitudesValidator.TryParseDiasPersonalizados(rawDiasMax, out var maxVal) ? maxVal : 999;

        return minEf == 0
            ? (true, null, null, SumarDiasSeguro(hoyMexico, maxEf))
            : (true, null, SumarDiasSeguro(hoyMexico, minEf), SumarDiasSeguro(hoyMexico, maxEf));
    }

    private static DateOnly SumarDiasSeguro(DateOnly fecha, int dias) =>
        DateOnly.MaxValue.DayNumber - fecha.DayNumber <= dias
            ? DateOnly.MaxValue
            : fecha.AddDays(dias);

    private static OrdenListadoSolicitudesCampo ResolverOrdenCampo(string? rawSort)
    {
        if (string.IsNullOrWhiteSpace(rawSort))
        {
            return OrdenListadoSolicitudesCampo.Vencimiento;
        }

        return rawSort.Trim().ToLowerInvariant() switch
        {
            "folio" => OrdenListadoSolicitudesCampo.Folio,
            "entrega" => OrdenListadoSolicitudesCampo.Entrega,
            "dias" => OrdenListadoSolicitudesCampo.Dias,
            _ => OrdenListadoSolicitudesCampo.Vencimiento
        };
    }
}
