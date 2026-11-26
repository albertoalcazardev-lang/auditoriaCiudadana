using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Solicitudes.Eliminar;

/// <summary>
/// Handler CQRS encargado de orquestar la eliminación controlada de una solicitud bajo precondición If-Match.
/// </summary>
public sealed class EliminarSolicitudHandler(
    ISolicitudEliminacionPersistence persistence,
    TimeProvider timeProvider)
    : IRequestHandler<EliminarSolicitudCommand, Result>
{
    public async Task<Result> Handle(
        EliminarSolicitudCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VersionConcurrencia))
        {
            return Result.Failure(
                Error.PreconditionRequired(
                    "Solicitud.VersionRequerida",
                    "El encabezado If-Match con la versión vigente de la solicitud es obligatorio para eliminarla."));
        }

        SolicitudInputParser.TryParseGuidNoVacio(request.SolicitudId, out var solicitudId);
        var (ahoraUtc, _) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);

        var persistRequest = new EliminacionSolicitudPersistRequest(
            solicitudId,
            request.VersionConcurrencia,
            ahoraUtc);

        var outcome = await persistence.EjecutarEliminacionAsync(persistRequest, cancellationToken);

        return outcome.Estado switch
        {
            EliminacionSolicitudEstado.Eliminada => Result.Success(),
            EliminacionSolicitudEstado.NoEncontrada => Result.Failure(
                Error.NotFound(
                    "Solicitud.NoEncontrada",
                    "La solicitud que intentas eliminar no existe o ya no está disponible.")),
            EliminacionSolicitudEstado.PrecondicionFallida => Result.Failure(
                Error.PreconditionFailed(
                    "Solicitud.PrecondicionConcurrenciaFallida",
                    "La solicitud fue modificada por otro proceso y su versión cambió. Recarga para ver el estado actual.")),
            EliminacionSolicitudEstado.ConflictoIntegridad => Result.Failure(
                Error.Conflict(
                    "Solicitud.ConflictoIntegridad",
                    "No es posible eliminar la solicitud porque tiene información relacionada vinculada.")),
            _ => Result.Failure(
                Error.Failure(
                    "Persistence.Unavailable",
                    "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."))
        };
    }
}
