using FluentValidation;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Validador de entrada para <see cref="CrearSolicitudCommand"/> sin consultas a base de datos.
/// </summary>
public sealed class CrearSolicitudValidator : AbstractValidator<CrearSolicitudCommand>
{
    public CrearSolicitudValidator()
    {
        RuleFor(x => x.UsuarioId).AplicarReglasUsuarioId();

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

        RuleFor(x => x.FechaEntrega).AplicarReglasFechaEntrega();
        RuleFor(x => x.HoraEntrega).AplicarReglasHoraEntrega();
        RuleFor(x => x.AreaReceptoraId).AplicarReglasAreaReceptoraId();
        RuleFor(x => x.Asunto).AplicarReglasAsunto();
        RuleFor(x => x.MedioEntrega).AplicarReglasMedioEntrega();
        RuleFor(x => x.EstatusEntrega).AplicarReglasEstatusEntrega();
        RuleFor(x => x.TipoSolicitud).AplicarReglasTipoSolicitud();
        RuleFor(x => x.TipoEvidencia).AplicarReglasTipoEvidencia();
        RuleFor(x => x.TieneAcuse).AplicarReglasTieneAcuse();
        RuleFor(x => x.TipoAcuse).AplicarReglasTipoAcuse();
        RuleFor(x => x).AplicarReglasCoherenciaAcuse(x => x.TieneAcuse, x => x.TipoAcuse);
        RuleFor(x => x.FechaVencimiento).AplicarReglasFechaVencimiento(x => x.FechaEntrega);
    }
}
