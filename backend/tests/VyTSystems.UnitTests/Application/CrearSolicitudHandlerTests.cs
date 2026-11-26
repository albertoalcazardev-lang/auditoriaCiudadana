using MapsterMapper;
using VyTSystems.Application;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Domain.Entities;

namespace VyTSystems.UnitTests.Application;

public sealed class CrearSolicitudHandlerTests
{
    private static readonly Guid AreaId = Guid.Parse("01950000-0000-7000-8000-000000000001");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubPersistence(
        Func<RegistroSolicitudPersistRequest, Func<string, Solicitud>, RegistroSolicitudOutcome> responder)
        : ISolicitudRegistroPersistence
    {
        public RegistroSolicitudPersistRequest? UltimoRequest { get; private set; }

        public Task<RegistroSolicitudOutcome> EjecutarCreacionAsync(
            RegistroSolicitudPersistRequest request,
            Func<string, Solicitud> construirSolicitud,
            CancellationToken cancellationToken = default)
        {
            UltimoRequest = request;
            return Task.FromResult(responder(request, construirSolicitud));
        }
    }

    private static IMapper CrearMapper() =>
        new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));

    private static CrearSolicitudCommand ComandoBase(bool automatico = false) => new(
        UsuarioId: "user-admin-01",
        IdempotencyKey: "key-123",
        GenerarFolioAutomatico: automatico,
        Folio: automatico ? null : "  001-AbC/2026 ",
        FechaEntrega: "2025-12-20",
        HoraEntrega: "11:45",
        AreaReceptoraId: AreaId.ToString(),
        Asunto: "  Asunto de prueba  ",
        MedioEntrega: "PortalWeb",
        EstatusEntrega: "EnProceso",
        TipoSolicitud: "Informacion",
        TipoEvidencia: "Link",
        TieneAcuse: true,
        TipoAcuse: "Electronico",
        FechaVencimiento: "2025-12-31");

    [Fact]
    public async Task Handle_CruceDeAnioEnCdmx_UsaAnioYHoyDeMexicoCity()
    {
        // 2026-01-01 04:30:00 UTC == 2025-12-31 22:30:00 en America/Mexico_City (UTC-6)
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 4, 30, 0, TimeSpan.Zero));
        Solicitud? entidadCreada = null;
        var persistence = new StubPersistence((req, construir) =>
        {
            var folio = Solicitud.FormatearFolioAutomatico(req.AnioFolioAutomatico, 7);
            entidadCreada = construir(folio);
            return RegistroSolicitudOutcome.Creada(entidadCreada);
        });

        var handler = new CrearSolicitudHandler(persistence, CrearMapper(), timeProvider);
        var result = await handler.Handle(ComandoBase(automatico: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.FueRecuperadaPorIdempotencia);
        Assert.NotNull(persistence.UltimoRequest);
        Assert.Equal(2025, persistence.UltimoRequest!.AnioFolioAutomatico);
        Assert.Equal("SOL-2025-007", result.Value.Solicitud.Folio);
        Assert.Equal(0, result.Value.Solicitud.DiasRestantes);
        Assert.Equal("EnProceso", result.Value.Solicitud.Estatus);
        Assert.NotNull(entidadCreada);
        Assert.Equal(new DateTime(2026, 1, 1, 4, 30, 0, DateTimeKind.Utc), entidadCreada!.CreadoEnUtc);
        Assert.True(entidadCreada.EsFolioAutomatico);
    }

    [Fact]
    public async Task Handle_ModoManualYRecuperadaPorIdempotencia_MapeaCorrectamente()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2025, 12, 26, 18, 0, 0, TimeSpan.Zero));
        var persistence = new StubPersistence((req, construir) =>
        {
            var s = construir(req.FolioManual!);
            return RegistroSolicitudOutcome.RecuperadaPorIdempotencia(s);
        });

        var handler = new CrearSolicitudHandler(persistence, CrearMapper(), timeProvider);
        var result = await handler.Handle(ComandoBase(automatico: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.FueRecuperadaPorIdempotencia);
        Assert.Equal("001-AbC/2026", result.Value.Solicitud.Folio);
        Assert.Equal(5, result.Value.Solicitud.DiasRestantes);
        Assert.Equal("EnProceso", result.Value.Solicitud.Estatus);
    }

    [Fact]
    public async Task Handle_MapeaErroresFuncionalesYDePersistenciaConCodigosExactos()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        var mapper = CrearMapper();

        var folioDup = await new CrearSolicitudHandler(
            new StubPersistence((_, _) => RegistroSolicitudOutcome.FolioDuplicado()),
            mapper,
            timeProvider).Handle(ComandoBase(), CancellationToken.None);
        Assert.True(folioDup.IsFailure);
        Assert.Equal(ErrorType.Conflict, folioDup.Error.Type);
        Assert.Equal("Solicitud.FolioDuplicado", folioDup.Error.Code);

        var idempConflict = await new CrearSolicitudHandler(
            new StubPersistence((_, _) => RegistroSolicitudOutcome.IdempotenciaConflicto()),
            mapper,
            timeProvider).Handle(ComandoBase(), CancellationToken.None);
        Assert.True(idempConflict.IsFailure);
        Assert.Equal(ErrorType.Conflict, idempConflict.Error.Type);
        Assert.Equal("Solicitud.IdempotenciaConflicto", idempConflict.Error.Code);

        var areaInvalida = await new CrearSolicitudHandler(
            new StubPersistence((_, _) => RegistroSolicitudOutcome.AreaReceptoraInvalida()),
            mapper,
            timeProvider).Handle(ComandoBase(), CancellationToken.None);
        Assert.True(areaInvalida.IsFailure);
        Assert.Equal(ErrorType.Validation, areaInvalida.Error.Type);
        Assert.Contains(areaInvalida.ValidationErrors, e => e.PropertyName == "AreaReceptoraId");

        var unavailable = await new CrearSolicitudHandler(
            new StubPersistence((_, _) => RegistroSolicitudOutcome.PersistenciaNoDisponible()),
            mapper,
            timeProvider).Handle(ComandoBase(), CancellationToken.None);
        Assert.True(unavailable.IsFailure);
        Assert.Equal(ErrorType.Failure, unavailable.Error.Type);
        Assert.Equal("Persistence.Unavailable", unavailable.Error.Code);
    }
}
