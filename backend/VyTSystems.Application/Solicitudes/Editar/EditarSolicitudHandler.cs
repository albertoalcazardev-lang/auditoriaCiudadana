using MapsterMapper;
using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Common.Validation;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Editar;

/// <summary>
/// Handler CQRS encargado de orquestar la edición atómica y optimista de una solicitud existente.
/// </summary>
public sealed class EditarSolicitudHandler(
    ISolicitudEdicionPersistence persistence,
    IMapper mapper,
    TimeProvider timeProvider)
    : IRequestHandler<EditarSolicitudCommand, Result<EditarSolicitudResult>>
{
    public async Task<Result<EditarSolicitudResult>> Handle(
        EditarSolicitudCommand request,
        CancellationToken cancellationToken)
    {
        SolicitudInputParser.TryParseGuidNoVacio(request.SolicitudId, out var solicitudId);
        SolicitudInputParser.TryParseFecha(request.FechaEntrega, out var fechaEntrega);
        SolicitudInputParser.TryParseHora(request.HoraEntrega, out var horaEntrega);
        SolicitudInputParser.TryParseAreaReceptoraId(request.AreaReceptoraId, out var areaReceptoraId);
        SolicitudInputParser.TryParseEnumName<MedioEntrega>(request.MedioEntrega, out var medioEntrega);
        SolicitudInputParser.TryParseEnumName<EstatusEntrega>(request.EstatusEntrega, out var estatusEntrega);
        SolicitudInputParser.TryParseEnumName<TipoSolicitud>(request.TipoSolicitud, out var tipoSolicitud);
        SolicitudInputParser.TryParseEnumName<TipoEvidencia>(request.TipoEvidencia, out var tipoEvidencia);
        SolicitudInputParser.TryParseEnumName<TipoAcuse>(request.TipoAcuse, out var tipoAcuse);
        SolicitudInputParser.TryParseFecha(request.FechaVencimiento, out var fechaVencimiento);

        var versionConcurrencia = request.VersionConcurrencia!;
        var asunto = request.Asunto!.Trim();
        var tieneAcuse = request.TieneAcuse!.Value;

        var persistRequest = new EdicionSolicitudPersistRequest(
            solicitudId,
            versionConcurrencia,
            areaReceptoraId);

        var outcome = await persistence.EjecutarEdicionAsync(
            persistRequest,
            solicitud => solicitud.ActualizarDatos(
                fechaEntrega,
                horaEntrega,
                areaReceptoraId,
                asunto,
                medioEntrega,
                estatusEntrega,
                tipoSolicitud,
                tipoEvidencia,
                tieneAcuse,
                tipoAcuse,
                fechaVencimiento),
            cancellationToken);

        return outcome.Estado switch
        {
            EdicionSolicitudEstado.Actualizada => MapearExito(outcome),
            EdicionSolicitudEstado.NoEncontrada => Result.Failure<EditarSolicitudResult>(
                Error.NotFound("Solicitud.NoEncontrada", "La solicitud solicitada no existe o ya no está disponible.")),
            EdicionSolicitudEstado.ConflictoConcurrencia => Result.Failure<EditarSolicitudResult>(
                Error.Conflict("Solicitud.ConflictoConcurrencia", "Otro proceso modificó esta solicitud. Recarga para ver los datos vigentes.")),
            EdicionSolicitudEstado.AreaReceptoraInvalida => Result.ValidationFailure<EditarSolicitudResult>(
                [new ValidationError(nameof(EditarSolicitudCommand.AreaReceptoraId), "El área receptora seleccionada no existe o no está activa.")]),
            _ => Result.Failure<EditarSolicitudResult>(
                Error.Failure("Persistence.Unavailable", "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."))
        };
    }

    private Result<EditarSolicitudResult> MapearExito(EdicionSolicitudOutcome outcome)
    {
        var (_, hoyMexico) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);
        var dto = SolicitudZonaHoraria.MapearSolicitudEdicion(mapper, outcome.Solicitud!, hoyMexico);
        return Result.Success(new EditarSolicitudResult(dto, outcome.VersionConcurrenciaActual!));
    }
}
