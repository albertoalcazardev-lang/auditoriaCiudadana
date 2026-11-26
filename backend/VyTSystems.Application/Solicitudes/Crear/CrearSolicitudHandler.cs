using MapsterMapper;
using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Common.Validation;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Handler CQRS encargado de orquestar la creación idempotente de una solicitud.
/// </summary>
public sealed class CrearSolicitudHandler(
    ISolicitudRegistroPersistence persistence,
    IMapper mapper,
    TimeProvider timeProvider)
    : IRequestHandler<CrearSolicitudCommand, Result<CrearSolicitudResult>>
{
    public async Task<Result<CrearSolicitudResult>> Handle(
        CrearSolicitudCommand request,
        CancellationToken cancellationToken)
    {
        SolicitudInputParser.TryParseFecha(request.FechaEntrega, out var fechaEntrega);
        SolicitudInputParser.TryParseHora(request.HoraEntrega, out var horaEntrega);
        SolicitudInputParser.TryParseAreaReceptoraId(request.AreaReceptoraId, out var areaReceptoraId);
        SolicitudInputParser.TryParseEnumName<MedioEntrega>(request.MedioEntrega, out var medioEntrega);
        SolicitudInputParser.TryParseEnumName<EstatusEntrega>(request.EstatusEntrega, out var estatusEntrega);
        SolicitudInputParser.TryParseEnumName<TipoSolicitud>(request.TipoSolicitud, out var tipoSolicitud);
        SolicitudInputParser.TryParseEnumName<TipoEvidencia>(request.TipoEvidencia, out var tipoEvidencia);
        SolicitudInputParser.TryParseEnumName<TipoAcuse>(request.TipoAcuse, out var tipoAcuse);
        SolicitudInputParser.TryParseFecha(request.FechaVencimiento, out var fechaVencimiento);

        var usuarioId = request.UsuarioId.Trim();
        var asunto = request.Asunto!.Trim();
        var tieneAcuse = request.TieneAcuse!.Value;

        var (ahoraUtc, hoy) = SolicitudZonaHoraria.ObtenerAhoraYHoyMexico(timeProvider);
        var anioActual = hoy.Year;

        var payloadHash = SolicitudIdempotencyHasher.CalcularHash(
            request.GenerarFolioAutomatico,
            request.Folio,
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
            fechaVencimiento);

        var folioManual = request.GenerarFolioAutomatico ? null : request.Folio!.Trim();

        var persistRequest = new RegistroSolicitudPersistRequest(
            usuarioId,
            request.IdempotencyKey!,
            payloadHash,
            request.GenerarFolioAutomatico,
            folioManual,
            areaReceptoraId,
            anioActual);

        var outcome = await persistence.EjecutarCreacionAsync(
            persistRequest,
            folioAsignado => Solicitud.Crear(
                folioAsignado,
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
                fechaVencimiento,
                usuarioId,
                ahoraUtc,
                request.GenerarFolioAutomatico),
            cancellationToken);

        return outcome.Estado switch
        {
            RegistroSolicitudEstado.Creada => MapearExito(outcome.Solicitud!, hoy, fueRecuperadaPorIdempotencia: false),
            RegistroSolicitudEstado.RecuperadaPorIdempotencia => MapearExito(outcome.Solicitud!, hoy, fueRecuperadaPorIdempotencia: true),
            RegistroSolicitudEstado.FolioDuplicado => Result.Failure<CrearSolicitudResult>(
                Error.Conflict("Solicitud.FolioDuplicado", "El folio ingresado ya está registrado. Introduce uno diferente.")),
            RegistroSolicitudEstado.IdempotenciaConflicto => Result.Failure<CrearSolicitudResult>(
                Error.Conflict("Solicitud.IdempotenciaConflicto", "La clave de idempotencia ya fue utilizada con una solicitud diferente.")),
            RegistroSolicitudEstado.AreaReceptoraInvalida => Result.ValidationFailure<CrearSolicitudResult>(
                [new ValidationError("AreaReceptoraId", "El área receptora seleccionada no existe o no está activa.")]),
            _ => Result.Failure<CrearSolicitudResult>(
                Error.Failure("Persistence.Unavailable", "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."))
        };
    }

    private Result<CrearSolicitudResult> MapearExito(
        Solicitud solicitud,
        DateOnly hoy,
        bool fueRecuperadaPorIdempotencia)
    {
        var dtoBase = mapper.Map<SolicitudCreadaDto>(solicitud);
        var dto = dtoBase with
        {
            DiasRestantes = solicitud.CalcularDiasRestantes(hoy),
            Estatus = solicitud.ObtenerEstatusEfectivo(hoy).ToString()
        };

        return Result.Success(new CrearSolicitudResult(dto, fueRecuperadaPorIdempotencia));
    }
}
