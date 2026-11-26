using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application;
using VyTSystems.Application.Common.Behaviors;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Crear;

namespace VyTSystems.UnitTests.Application;

public sealed class SolicitudMappingAndPipelineTests
{
    [Fact]
    public void MappingConfig_CompilaSinErrores()
    {
        var config = MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly);
        config.Compile();
    }

    [Fact]
    public void AddApplication_RegistraTimeProviderValidatorsYBehavior()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(TimeProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<CrearSolicitudCommand>));
        Assert.Contains(services, d => d.ServiceType == typeof(IPipelineBehavior<,>)
            && d.ImplementationType == typeof(ValidationBehavior<,>));
    }

    [Fact]
    public async Task ValidationBehavior_ConComandoInvalido_LanzaValidationExceptionSinEjecutarNext()
    {
        var behavior = new ValidationBehavior<CrearSolicitudCommand, Result<CrearSolicitudResult>>(
            [new CrearSolicitudValidator()]);

        var nextInvocado = false;
        var invalido = new CrearSolicitudCommand(
            UsuarioId: "user-1",
            IdempotencyKey: "",
            GenerarFolioAutomatico: false,
            Folio: "",
            FechaEntrega: "bad",
            HoraEntrega: "bad",
            AreaReceptoraId: "bad",
            Asunto: "",
            MedioEntrega: "1",
            EstatusEntrega: "Vencida",
            TipoSolicitud: "bad",
            TipoEvidencia: "bad",
            TieneAcuse: false,
            TipoAcuse: "Sello",
            FechaVencimiento: "bad");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(invalido, _ =>
            {
                nextInvocado = true;
                return Task.FromResult(Result.Failure<CrearSolicitudResult>(Error.Failure("Test", "No")));
            }, CancellationToken.None));

        Assert.False(nextInvocado);
        Assert.NotEmpty(ex.Errors);
        Assert.All(ex.Errors, failure => Assert.Null(failure.AttemptedValue));
    }
}
