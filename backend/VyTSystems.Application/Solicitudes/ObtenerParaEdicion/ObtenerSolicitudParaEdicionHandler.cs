using MapsterMapper;
using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

/// <summary>
/// Handler CQRS encargado de recuperar una solicitud existente por folio normalizado para precarga de edición.
/// </summary>
public sealed class ObtenerSolicitudParaEdicionHandler(
    ISolicitudEdicionReadRepository readRepository,
    IMapper mapper,
    TimeProvider timeProvider)
    : IRequestHandler<ObtenerSolicitudParaEdicionQuery, Result<ObtenerSolicitudParaEdicionResult>>
{
    public async Task<Result<ObtenerSolicitudParaEdicionResult>> Handle(
        ObtenerSolicitudParaEdicionQuery request,
        CancellationToken cancellationToken)
    {
        var folioNormalizado = Solicitud.NormalizarFolio(request.Folio!);
        var outcome = await readRepository.ObtenerPorFolioNormalizadoAsync(folioNormalizado, cancellationToken);

        if (outcome.Estado == LecturaSolicitudEdicionEstado.Encontrada)
        {
            var (_, hoyMexico) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);
            var dto = SolicitudZonaHoraria.MapearSolicitudEdicion(mapper, outcome.Solicitud!, hoyMexico);
            return Result.Success(new ObtenerSolicitudParaEdicionResult(dto, outcome.VersionConcurrencia!));
        }

        if (outcome.Estado == LecturaSolicitudEdicionEstado.NoEncontrada)
        {
            return Result.Failure<ObtenerSolicitudParaEdicionResult>(
                Error.NotFound("Solicitud.NoEncontrada", "No se encontró ninguna solicitud con el folio indicado."));
        }

        return Result.Failure<ObtenerSolicitudParaEdicionResult>(
            Error.Failure(
                "Persistence.Unavailable",
                "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."));
    }
}
