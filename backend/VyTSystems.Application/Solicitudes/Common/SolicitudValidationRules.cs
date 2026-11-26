using FluentValidation;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Common;

/// <summary>
/// Reglas planas de FluentValidation compartidas entre la creación y la edición de solicitudes.
/// </summary>
internal static class SolicitudValidationRules
{
    public static IRuleBuilderOptions<T, string> AplicarReglasUsuarioId<T>(
        this IRuleBuilderInitial<T, string> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El usuario autenticado es obligatorio.")
            .Must(id => !id!.Any(char.IsControl) && id!.Trim().Length <= Solicitud.MaxUsuarioIdLength)
            .WithMessage($"El identificador del usuario no puede contener caracteres de control ni exceder {Solicitud.MaxUsuarioIdLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasFechaEntrega<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La fecha de entrega es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
            .WithMessage("La fecha de entrega debe tener el formato válido yyyy-MM-dd.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasHoraEntrega<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La hora de entrega es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseHora(raw, out _))
            .WithMessage("La hora de entrega debe tener formato 24 horas HH:mm.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasAreaReceptoraId<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El área receptora es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseAreaReceptoraId(raw, out _))
            .WithMessage("El identificador del área receptora no es válido.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasAsunto<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El asunto es obligatorio.")
            .Must(a => !a!.Any(char.IsControl) && a!.Trim().Length <= Solicitud.MaxAsuntoLength)
            .WithMessage($"El asunto no puede contener caracteres de control ni exceder {Solicitud.MaxAsuntoLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasMedioEntrega<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El medio de entrega es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<MedioEntrega>(raw, out _))
            .WithMessage("Selecciona un medio de entrega válido.");

    public static IRuleBuilderOptionsConditions<T, string?> AplicarReglasEstatusEntrega<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El estatus de entrega es obligatorio.")
            .Custom((raw, context) =>
            {
                if (string.Equals(raw?.Trim(), nameof(EstatusEfectivo.Vencida), StringComparison.OrdinalIgnoreCase))
                {
                    context.AddFailure("EstatusEntrega",
                        "El estatus 'Vencida' se calcula automáticamente y no puede capturarse al registrar.");
                    return;
                }

                if (!SolicitudInputParser.TryParseEnumName<EstatusEntrega>(raw, out _))
                {
                    context.AddFailure("EstatusEntrega",
                        "Selecciona un estatus de entrega válido (EnProceso o Entregada).");
                }
            });

    public static IRuleBuilderOptions<T, string?> AplicarReglasTipoSolicitud<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de solicitud es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoSolicitud>(raw, out _))
            .WithMessage("Selecciona un tipo de solicitud válido.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasTipoEvidencia<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de evidencia es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoEvidencia>(raw, out _))
            .WithMessage("Selecciona un tipo de evidencia válido.");

    public static IRuleBuilderOptions<T, bool?> AplicarReglasTieneAcuse<T>(
        this IRuleBuilderInitial<T, bool?> ruleBuilder) =>
        ruleBuilder
            .NotNull().WithMessage("Indica si la solicitud cuenta con acuse.");

    public static IRuleBuilderOptions<T, string?> AplicarReglasTipoAcuse<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El tipo de acuse es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseEnumName<TipoAcuse>(raw, out _))
            .WithMessage("Selecciona un tipo de acuse válido.");

    public static IRuleBuilderOptionsConditions<T, T> AplicarReglasCoherenciaAcuse<T>(
        this IRuleBuilderInitial<T, T> ruleBuilder,
        Func<T, bool?> obtenerTieneAcuse,
        Func<T, string?> obtenerTipoAcuse) =>
        ruleBuilder.Custom((instance, context) =>
        {
            var tieneAcuse = obtenerTieneAcuse(instance);
            var rawTipoAcuse = obtenerTipoAcuse(instance);

            if (!tieneAcuse.HasValue ||
                !SolicitudInputParser.TryParseEnumName<TipoAcuse>(rawTipoAcuse, out var tipoAcuse))
            {
                return;
            }

            if (!tieneAcuse.Value && tipoAcuse != TipoAcuse.NoAplica)
            {
                context.AddFailure("TipoAcuse",
                    "Cuando no se cuenta con acuse, el tipo de acuse debe ser NoAplica.");
            }
            else if (tieneAcuse.Value && tipoAcuse == TipoAcuse.NoAplica)
            {
                context.AddFailure("TipoAcuse",
                    "Cuando se cuenta con acuse, selecciona un tipo distinto a NoAplica.");
            }
        });

    public static IRuleBuilderOptions<T, string?> AplicarReglasFechaVencimiento<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder,
        Func<T, string?> obtenerFechaEntrega) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La fecha de vencimiento es obligatoria.")
            .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
            .WithMessage("La fecha de vencimiento debe tener el formato válido yyyy-MM-dd.")
            .Must((instance, raw) =>
            {
                if (!SolicitudInputParser.TryParseFecha(obtenerFechaEntrega(instance), out var fechaEntrega) ||
                    !SolicitudInputParser.TryParseFecha(raw, out var fechaVencimiento))
                {
                    return true;
                }

                return fechaVencimiento >= fechaEntrega;
            })
            .WithMessage("La fecha de vencimiento no puede ser anterior a la fecha de entrega.");
}
