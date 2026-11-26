using MapsterMapper;
using VyTSystems.Application;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Editar;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.UnitTests.Application;

public sealed class EditarSolicitudHandlerTests
{
    private static readonly Guid AreaInicialId = Guid.Parse("01950000-0000-7000-8000-000000000001");
    private static readonly Guid AreaNuevaId = Guid.Parse("01950000-0000-7000-8000-000000000003");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubEdicionPersistence(
        Func<EdicionSolicitudPersistRequest, Action<Solicitud>, EdicionSolicitudOutcome> responder)
        : ISolicitudEdicionPersistence
    {
        public EdicionSolicitudPersistRequest? UltimoRequest { get; private set; }

        public Task<EdicionSolicitudOutcome> EjecutarEdicionAsync(
            EdicionSolicitudPersistRequest request,
            Action<Solicitud> actualizarSolicitud,
            CancellationToken cancellationToken = default)
        {
            UltimoRequest = request;
            return Task.FromResult(responder(request, actualizarSolicitud));
        }
    }

    private static IMapper CrearMapper() =>
        new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));

    private static Solicitud CrearEntidadOriginal() =>
        Solicitud.Crear(
            "0042-AbC/2026",
            new DateOnly(2026, 5, 1),
            new TimeOnly(10, 0),
            AreaInicialId,
            "Asunto original",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 10),
            "admin-creador",
            new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc));

    private static EditarSolicitudCommand ComandoEdicion(Guid solicitudId) => new(
        UsuarioId: "admin-editor",
        SolicitudId: solicitudId.ToString(),
        VersionConcurrencia: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        FechaEntrega: "2026-05-05",
        HoraEntrega: "16:20",
        AreaReceptoraId: AreaNuevaId.ToString(),
        Asunto: "  Asunto modificado en edición  ",
        MedioEntrega: "Impreso",
        EstatusEntrega: "EnProceso",
        TipoSolicitud: "Aclaracion",
        TipoEvidencia: "Link",
        TieneAcuse: false,
        TipoAcuse: "NoAplica",
        FechaVencimiento: "2026-05-15");

    [Fact]
    public async Task Handle_Actualizada_MutaEntidadPreservaFolioYRecalculaEstatusEfectivo()
    {
        var entidad = CrearEntidadOriginal();
        const string nuevaVersion = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        // 2026-05-16 14:00 UTC == 2026-05-16 08:00 CDMX -> mayor que FechaVencimiento (2026-05-15) => Vencida
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 5, 16, 14, 0, 0, TimeSpan.Zero));
        var persistence = new StubEdicionPersistence((_, mutar) =>
        {
            mutar(entidad);
            return EdicionSolicitudOutcome.Actualizada(entidad, nuevaVersion);
        });

        var handler = new EditarSolicitudHandler(persistence, CrearMapper(), timeProvider);
        var result = await handler.Handle(ComandoEdicion(entidad.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persistence.UltimoRequest);
        Assert.Equal(entidad.Id, persistence.UltimoRequest!.SolicitudId);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", persistence.UltimoRequest.VersionConcurrenciaEsperada);
        Assert.Equal(AreaNuevaId, persistence.UltimoRequest.AreaReceptoraId);

        Assert.Equal(nuevaVersion, result.Value.VersionConcurrencia);
        Assert.Equal("0042-AbC/2026", result.Value.Solicitud.Folio);
        Assert.Equal("Asunto modificado en edición", result.Value.Solicitud.Asunto);
        Assert.Equal("Impreso", result.Value.Solicitud.MedioEntrega);
        Assert.False(result.Value.Solicitud.TieneAcuse);
        Assert.Equal("NoAplica", result.Value.Solicitud.TipoAcuse);
        Assert.Equal(0, result.Value.Solicitud.DiasRestantes);
        Assert.Equal("Vencida", result.Value.Solicitud.EstatusEfectivo);
        Assert.Equal("admin-creador", entidad.CreadoPorUsuarioId);
    }

    [Fact]
    public async Task Handle_MapeaNoEncontradaConflictoAreaInvalidaYPersistenciaNoDisponible()
    {
        var id = Guid.Parse("01950000-0000-7000-8000-000000000099");
        var cmd = ComandoEdicion(id);
        var mapper = CrearMapper();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        var r404 = await new EditarSolicitudHandler(
            new StubEdicionPersistence((_, _) => EdicionSolicitudOutcome.NoEncontrada()),
            mapper,
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r404.IsFailure);
        Assert.Equal(ErrorType.NotFound, r404.Error.Type);
        Assert.Equal("Solicitud.NoEncontrada", r404.Error.Code);

        var r409 = await new EditarSolicitudHandler(
            new StubEdicionPersistence((_, _) => EdicionSolicitudOutcome.ConflictoConcurrencia()),
            mapper,
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r409.IsFailure);
        Assert.Equal(ErrorType.Conflict, r409.Error.Type);
        Assert.Equal("Solicitud.ConflictoConcurrencia", r409.Error.Code);

        var rArea = await new EditarSolicitudHandler(
            new StubEdicionPersistence((_, _) => EdicionSolicitudOutcome.AreaReceptoraInvalida()),
            mapper,
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(rArea.IsFailure);
        Assert.Equal(ErrorType.Validation, rArea.Error.Type);
        Assert.Contains(rArea.ValidationErrors, e => e.PropertyName == nameof(EditarSolicitudCommand.AreaReceptoraId));

        var r503 = await new EditarSolicitudHandler(
            new StubEdicionPersistence((_, _) => EdicionSolicitudOutcome.PersistenciaNoDisponible()),
            mapper,
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r503.IsFailure);
        Assert.Equal(ErrorType.Failure, r503.Error.Type);
        Assert.Equal("Persistence.Unavailable", r503.Error.Code);
    }
}
