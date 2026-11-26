using FluentValidation;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Validador de entrada para <see cref="CrearSolicitudCommand"/> sin consultas a base de datos.
/// </summary>
public sealed class CrearSolicitudValidator : AbstractValidator<CrearSolicitudCommand>
{
    public CrearSolicitudValidator()
    {
        RuleFor(x => x.UsuarioId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El usuario autenticado es obligatorio.")
            .Must(id => !id!.Any(char.IsControl) && id!.Trim().Length <= Solicitud.MaxUsuarioIdLength)
            .WithMessage($"El identificador del usuario no puede contener caracteres de control ni exceder {Solicitud.MaxUsuarioIdLength} caracteres.");

        RuleFor(x => x.IdempotencyKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El encabezado Idempotency-Key es obligatorio.")
            .Must(SolicitudInputParser.EsIdempotencyKeyValida)
            .WithMessage("El encabezado Idempotency-Key debe tener entre 1 y 100 caracteres válidos (A-Z, a-z, 0-9, '.', '_', ':', '-').");

        When(x => x.GenerarFolioAutomatico, () =>
        {
            RuleFor(x => x.Folio)
                .Must(folio => folio is null)
                .WithMessage("No debes proporcionar un folio manual cuando está activada la generación automática.");
        });

        When(x => !x.GenerarFolioAutomatico, () =>
        {
            RuleFor(x => x.Folio)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("El folio es obligatorio cuando no se genera automáticamente.")
                .Must(Solicitud.EsFolioValido)
                .WithMessage($"El folio debe tener entre 1 y {Solicitud.MaxFolioLength} caracteres válidos (letras, números, '.', '_', '/', '-'), incluir al menos un carácter alfanumérico y no contener caracteres de control.");
        });

        RuleFor(x => x.FechaEntrega)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La fecha de entrega es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
            .WithMessage("La fecha de entrega debe tener el formato válido yyyy-MM-dd.");

        RuleFor(x => x.HoraEntrega)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La hora de entrega es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseHora(raw, out _))
            .WithMessage("La hora de entrega debe tener formato 24 horas HH:mm.");

        RuleFor(x => x.AreaReceptoraId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El área receptora es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseAreaReceptoraId(raw, out _))
            .WithMessage("El identificador del área receptora no es válido.");

        RuleFor(x => x.Asunto)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El asunto es obligatorio.")
            .Must(a => !a!.Any(char.IsControl) && a!.Trim().Length <= Solicitud.MaxAsuntoLength)
            .WithMessage($"El asunto no puede contener caracteres de control ni exceder {Solicitud.MaxAsuntoLength} caracteres.");

        RuleFor(x => x.MedioEntrega)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El medio de entrega es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<MedioEntrega>(raw, out _))
            .WithMessage("Selecciona un medio de entrega válido.");

        RuleFor(x => x.EstatusEntrega)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El estatus de entrega es obligatorio.")
            .Custom((raw, context) =>
            {
                if (string.Equals(raw?.Trim(), nameof(EstatusEfectivo.Vencida), StringComparison.OrdinalIgnoreCase))
                {
                    context.AddFailure(nameof(CrearSolicitudCommand.EstatusEntrega),
                        "El estatus 'Vencida' se calcula automáticamente y no puede capturarse al registrar.");
                    return;
                }

                if (!SolicitudInputParser.TryParseEnumName<EstatusEntrega>(raw, out _))
                {
                    context.AddFailure(nameof(CrearSolicitudCommand.EstatusEntrega),
                        "Selecciona un estatus de entrega válido (EnProceso o Entregada).");
                }
            });

        RuleFor(x => x.TipoSolicitud)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de solicitud es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoSolicitud>(raw, out _))
            .WithMessage("Selecciona un tipo de solicitud válido.");

        RuleFor(x => x.TipoEvidencia)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de evidencia es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoEvidencia>(raw, out _))
            .WithMessage("Selecciona un tipo de evidencia válido.");

        RuleFor(x => x.TieneAcuse)
            .NotNull().WithMessage("Indica si la solicitud cuenta con acuse.");

        RuleFor(x => x.TipoAcuse)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de acuse es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoAcuse>(raw, out _))
            .WithMessage("Selecciona un tipo de acuse válido.");

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                if (!command.TieneAcuse.HasValue ||
                    !SolicitudInputParser.TryParseEnumName<TipoAcuse>(command.TipoAcuse, out var tipoAcuse))
                {
                    return;
                }

                if (!command.TieneAcuse.Value && tipoAcuse != TipoAcuse.NoAplica)
                {
                    context.AddFailure(nameof(CrearSolicitudCommand.TipoAcuse),
                        "Cuando no se cuenta con acuse, el tipo de acuse debe ser NoAplica.");
                }
                else if (command.TieneAcuse.Value && tipoAcuse == TipoAcuse.NoAplica)
                {
                    context.AddFailure(nameof(CrearSolicitudCommand.TipoAcuse),
                        "Cuando se cuenta con acuse, selecciona un tipo distinto a NoAplica.");
                }
            });

        RuleFor(x => x.FechaVencimiento)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La fecha de vencimiento es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
            .WithMessage("La fecha de vencimiento debe tener el formato válido yyyy-MM-dd.")
            .Must((command, raw) =>
            {
                if (!SolicitudInputParser.TryParseFecha(command.FechaEntrega, out var fechaEntrega) ||
                    !SolicitudInputParser.TryParseFecha(raw, out var fechaVencimiento))
                {
                    return true;
                }

                return fechaVencimiento >= fechaEntrega;
            })
            .WithMessage("La fecha de vencimiento no puede ser anterior a la fecha de entrega.");
    }
}
