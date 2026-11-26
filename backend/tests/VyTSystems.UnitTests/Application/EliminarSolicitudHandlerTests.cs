using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Eliminar;

namespace VyTSystems.UnitTests.Application;

public sealed class EliminarSolicitudHandlerTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubEliminacionPersistence(
        Func<EliminacionSolicitudPersistRequest, EliminacionSolicitudOutcome> responder)
        : ISolicitudEliminacionPersistence
    {
        public EliminacionSolicitudPersistRequest? UltimoRequest { get; private set; }
        public int Invocaciones { get; private set; }

        public Task<EliminacionSolicitudOutcome> EjecutarEliminacionAsync(
            EliminacionSolicitudPersistRequest request,
            CancellationToken cancellationToken = default)
        {
            Invocaciones++;
            UltimoRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_SinVersionConcurrencia_RetornaPreconditionRequired428SinInvocarPersistencia(string? version)
    {
        var persistence = new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.Eliminada());
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var handler = new EliminarSolicitudHandler(persistence, clock);

        var result = await handler.Handle(
            new EliminarSolicitudCommand(
                "admin-01",
                "01950000-0000-7000-8000-000000000055",
                version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, persistence.Invocaciones);
        Assert.Equal(ErrorType.PreconditionRequired, result.Error.Type);
        Assert.Equal("Solicitud.VersionRequerida", result.Error.Code);
    }

    [Fact]
    public async Task Handle_MapeaExitoYTodosLosEstadosDeFalloConCodigosExactos()
    {
        var solicitudId = Guid.Parse("01950000-0000-7000-8000-000000000088");
        var utcInstante = new DateTime(2026, 5, 15, 19, 45, 0, DateTimeKind.Utc);
        var clock = new FixedTimeProvider(new DateTimeOffset(utcInstante));
        var cmd = new EliminarSolicitudCommand(
            "admin-01",
            solicitudId.ToString(),
            "0123456789abcdef0123456789abcdef");

        var exitoPersistence = new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.Eliminada());
        var r204 = await new EliminarSolicitudHandler(exitoPersistence, clock).Handle(cmd, CancellationToken.None);
        Assert.True(r204.IsSuccess);
        Assert.NotNull(exitoPersistence.UltimoRequest);
        Assert.Equal(solicitudId, exitoPersistence.UltimoRequest!.SolicitudId);
        Assert.Equal("0123456789abcdef0123456789abcdef", exitoPersistence.UltimoRequest.VersionConcurrenciaEsperada);
        Assert.Equal(utcInstante, exitoPersistence.UltimoRequest.EliminadoEnUtc);

        var r404 = await new EliminarSolicitudHandler(
            new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.NoEncontrada()),
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r404.IsFailure);
        Assert.Equal(ErrorType.NotFound, r404.Error.Type);
        Assert.Equal("Solicitud.NoEncontrada", r404.Error.Code);

        var r412 = await new EliminarSolicitudHandler(
            new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.PrecondicionFallida()),
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r412.IsFailure);
        Assert.Equal(ErrorType.PreconditionFailed, r412.Error.Type);
        Assert.Equal("Solicitud.PrecondicionConcurrenciaFallida", r412.Error.Code);

        var r409 = await new EliminarSolicitudHandler(
            new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.ConflictoIntegridad()),
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r409.IsFailure);
        Assert.Equal(ErrorType.Conflict, r409.Error.Type);
        Assert.Equal("Solicitud.ConflictoIntegridad", r409.Error.Code);

        var r503 = await new EliminarSolicitudHandler(
            new StubEliminacionPersistence(_ => EliminacionSolicitudOutcome.PersistenciaNoDisponible()),
            clock).Handle(cmd, CancellationToken.None);
        Assert.True(r503.IsFailure);
        Assert.Equal(ErrorType.Failure, r503.Error.Type);
        Assert.Equal("Persistence.Unavailable", r503.Error.Code);
    }
}
