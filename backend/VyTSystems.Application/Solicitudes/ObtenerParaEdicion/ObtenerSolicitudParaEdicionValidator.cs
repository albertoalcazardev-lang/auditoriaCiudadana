using FluentValidation;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

/// <summary>
/// Validador de entrada para <see cref="ObtenerSolicitudParaEdicionQuery"/> sin consultas a persistencia.
/// </summary>
public sealed class ObtenerSolicitudParaEdicionValidator : AbstractValidator<ObtenerSolicitudParaEdicionQuery>
{
    public ObtenerSolicitudParaEdicionValidator()
    {
        RuleFor(x => x.Folio)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El folio a buscar es obligatorio.")
            .Must(Solicitud.EsFolioValido)
            .WithMessage($"El folio debe tener entre 1 y {Solicitud.MaxFolioLength} caracteres válidos (letras, números, '.', '_', '/', '-'), incluir al menos un carácter alfanumérico y no contener caracteres de control.");
    }
}
