using FluentValidation;

namespace VyTSystems.Application.Dashboard.ObtenerResumen;

/// <summary>
/// Validador de entrada para <see cref="ObtenerDashboardQuery"/> garantizando que el periodo pertenezca al catálogo cerrado.
/// </summary>
public sealed class ObtenerDashboardValidator : AbstractValidator<ObtenerDashboardQuery>
{
    public ObtenerDashboardValidator()
    {
        RuleFor(x => x.Periodo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("El periodo de consulta es obligatorio.")
            .Must(p => !p!.Any(char.IsControl))
            .WithMessage("El parámetro de periodo tiene un formato inválido o fue enviado múltiples veces.")
            .Must(DashboardPeriodos.EsValido)
            .WithMessage(
                "El periodo indicado no es válido. Valores permitidos: este-mes, mes-anterior, ultimos-3-meses, este-anio, todo.");
    }
}
