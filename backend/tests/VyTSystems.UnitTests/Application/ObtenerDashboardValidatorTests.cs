using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application;
using VyTSystems.Application.Common.Behaviors;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Dashboard.ObtenerResumen;

namespace VyTSystems.UnitTests.Application;

public sealed class ObtenerDashboardValidatorTests
{
    private readonly ObtenerDashboardValidator _validator = new();

    [Theory]
    [InlineData(DashboardPeriodos.EsteMes)]
    [InlineData(DashboardPeriodos.MesAnterior)]
    [InlineData(DashboardPeriodos.Ultimos3Meses)]
    [InlineData(DashboardPeriodos.EsteAnio)]
    [InlineData(DashboardPeriodos.Todo)]
    public void Validate_ConLosCincoPeriodosValidos_PasaSinErrores(string periodo)
    {
        Assert.True(DashboardPeriodos.EsValido(periodo));

        var result = _validator.Validate(new ObtenerDashboardQuery(periodo));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\u0000")]
    [InlineData("este-mes\n")]
    [InlineData("este-mes\r")]
    [InlineData("este\u0000mes")]
    [InlineData("invalido")]
    [InlineData("ESTE-MES")]
    [InlineData("Este-Mes")]
    [InlineData(" este-mes")]
    [InlineData("este-mes ")]
    [InlineData("este_mes")]
    [InlineData("ultimos-6-meses")]
    public void Validate_ConPeriodoVacioDesconocidoOConControl_FallaEnPropiedadPeriodo(string? periodoInvalido)
    {
        Assert.False(DashboardPeriodos.EsValido(periodoInvalido));

        var result = _validator.Validate(new ObtenerDashboardQuery(periodoInvalido));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ObtenerDashboardQuery.Periodo), error.PropertyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("desconocido")]
    [InlineData("\u0000")]
    public async Task ValidationBehavior_ConPeriodoInvalidoODuplicado_CortaPipelineConPropiedadPeriodoSinAttemptedValue(
        string periodoInvalido)
    {
        var behavior = new ValidationBehavior<ObtenerDashboardQuery, Result<DashboardResumenDto>>([_validator]);
        var handlerEjecutado = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(
                new ObtenerDashboardQuery(periodoInvalido),
                _ =>
                {
                    handlerEjecutado = true;
                    return Task.FromResult(
                        Result.Failure<DashboardResumenDto>(Error.Failure("NoDebeEjecutarse", "No")));
                },
                CancellationToken.None));

        Assert.False(handlerEjecutado);
        var failure = Assert.Single(exception.Errors);
        Assert.Equal("Periodo", failure.PropertyName);
        Assert.Null(failure.AttemptedValue);
    }

    [Fact]
    public void AddApplication_RegistraObtenerDashboardValidator()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<ObtenerDashboardQuery>));
    }
}
