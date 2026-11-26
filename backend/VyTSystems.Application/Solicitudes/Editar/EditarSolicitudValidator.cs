using FluentValidation;
using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Solicitudes.Editar;

/// <summary>
/// Validador de entrada para <see cref="EditarSolicitudCommand"/> sin consultas a base de datos.
/// </summary>
public sealed class EditarSolicitudValidator : AbstractValidator<EditarSolicitudCommand>
{
    public EditarSolicitudValidator()
    {
        RuleFor(x => x.UsuarioId).AplicarReglasUsuarioId();

        RuleFor(x => x.SolicitudId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El identificador de la solicitud es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseGuidNoVacio(raw, out _))
            .WithMessage("El identificador de la solicitud no tiene un formato GUID válido.")
            .OverridePropertyName("Id");

        RuleFor(x => x.VersionConcurrencia)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El encabezado If-Match con la versión de la solicitud es obligatorio.")
            .Must(SolicitudInputParser.EsVersionConcurrenciaValida)
            .WithMessage("El encabezado If-Match debe contener un ETag fuerte válido.")
            .OverridePropertyName("IfMatch");

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
