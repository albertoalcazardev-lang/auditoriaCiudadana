using FluentValidation;
using VyTSystems.Application.Solicitudes.Common;

namespace VyTSystems.Application.Solicitudes.Eliminar;

/// <summary>
/// Validador de entrada para <see cref="EliminarSolicitudCommand"/> sin acceso a base de datos.
/// </summary>
public sealed class EliminarSolicitudValidator : AbstractValidator<EliminarSolicitudCommand>
{
    public EliminarSolicitudValidator()
    {
        RuleFor(x => x.UsuarioId).AplicarReglasUsuarioId();

        RuleFor(x => x.SolicitudId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El identificador de la solicitud es obligatorio.")
            .Must(raw => SolicitudInputParser.TryParseGuidNoVacio(raw, out _))
            .WithMessage("El identificador de la solicitud no tiene un formato GUID válido.")
            .OverridePropertyName("Id");

        When(x => x.VersionConcurrencia is not null, () =>
        {
            RuleFor(x => x.VersionConcurrencia)
                .Must(SolicitudInputParser.EsVersionConcurrenciaValida)
                .WithMessage("El encabezado If-Match debe contener un ETag fuerte válido.")
                .OverridePropertyName("IfMatch");
        });
    }
}
