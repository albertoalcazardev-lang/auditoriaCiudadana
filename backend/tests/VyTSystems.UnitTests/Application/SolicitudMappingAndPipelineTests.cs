using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application;
using VyTSystems.Application.Common.Behaviors;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Application.Solicitudes.Editar;
using VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

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
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<ObtenerSolicitudParaEdicionQuery>));
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<EditarSolicitudCommand>));
        Assert.Contains(services, d => d.ServiceType == typeof(IPipelineBehavior<,>)
            && d.ImplementationType == typeof(ValidationBehavior<,>));
    }

    [Fact]
    public async Task ValidationBehavior_ConComandoCrearInvalido_LanzaValidationExceptionSinEjecutarNext()
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

    [Fact]
    public async Task ValidationBehavior_ConEditarYObtenerParaEdicionInvalidos_CortaPipelineSinAttemptedValue()
    {
        var queryBehavior = new ValidationBehavior<ObtenerSolicitudParaEdicionQuery, Result<ObtenerSolicitudParaEdicionResult>>(
            [new ObtenerSolicitudParaEdicionValidator()]);

        var nextQuery = false;
        var exQuery = await Assert.ThrowsAsync<ValidationException>(() =>
            queryBehavior.Handle(new ObtenerSolicitudParaEdicionQuery("   "), _ =>
            {
                nextQuery = true;
                return Task.FromResult(Result.Failure<ObtenerSolicitudParaEdicionResult>(Error.Failure("Test", "No")));
            }, CancellationToken.None));
        Assert.False(nextQuery);
        Assert.Contains(exQuery.Errors, e => e.PropertyName == "Folio");
        Assert.All(exQuery.Errors, failure => Assert.Null(failure.AttemptedValue));

        var editBehavior = new ValidationBehavior<EditarSolicitudCommand, Result<EditarSolicitudResult>>(
            [new EditarSolicitudValidator()]);

        var nextEdit = false;
        var cmdInvalido = new EditarSolicitudCommand(
            UsuarioId: "admin-1",
            SolicitudId: "no-guid",
            VersionConcurrencia: "\u0000",
            FechaEntrega: "2026-05-10",
            HoraEntrega: "10:00",
            AreaReceptoraId: "01950000-0000-7000-8000-000000000001",
            Asunto: "Asunto válido",
            MedioEntrega: "Digital",
            EstatusEntrega: "EnProceso",
            TipoSolicitud: "Informacion",
            TipoEvidencia: "Carpeta",
            TieneAcuse: false,
            TipoAcuse: "NoAplica",
            FechaVencimiento: "2026-05-15");

        var exEdit = await Assert.ThrowsAsync<ValidationException>(() =>
            editBehavior.Handle(cmdInvalido, _ =>
            {
                nextEdit = true;
                return Task.FromResult(Result.Failure<EditarSolicitudResult>(Error.Failure("Test", "No")));
            }, CancellationToken.None));

        Assert.False(nextEdit);
        Assert.Contains(exEdit.Errors, e => e.PropertyName == "Id");
        Assert.Contains(exEdit.Errors, e => e.PropertyName == "IfMatch");
        Assert.All(exEdit.Errors, failure => Assert.Null(failure.AttemptedValue));
    }
}
