using MapsterMapper;
using VyTSystems.Application;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.ObtenerParaEdicion;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.UnitTests.Application;

public sealed class ObtenerSolicitudParaEdicionTests
{
    private static readonly Guid AreaId = Guid.Parse("01950000-0000-7000-8000-000000000002");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubReadRepository(
        Func<string, LecturaSolicitudEdicionOutcome> responder) : ISolicitudEdicionReadRepository
    {
        public string? UltimoFolioNormalizado { get; private set; }

        public Task<LecturaSolicitudEdicionOutcome> ObtenerPorFolioNormalizadoAsync(
            string folioNormalizado,
            CancellationToken cancellationToken = default)
        {
            UltimoFolioNormalizado = folioNormalizado;
            return Task.FromResult(responder(folioNormalizado));
        }
    }

    private static IMapper CrearMapper() =>
        new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));

    [Theory]
    [InlineData("SOL-2026-001")]
    [InlineData("  0012-AbC/2026_1.0  ")]
    public void Validator_ConFolioValido_Pasa(string folio)
    {
        var validator = new ObtenerSolicitudParaEdicionValidator();
        var result = validator.Validate(new ObtenerSolicitudParaEdicionQuery(folio));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("SOL 001")]
    [InlineData("SOL-001\n")]
    [InlineData("SOL\r001")]
    [InlineData("SOL\t001")]
    [InlineData("\0")]
    public void Validator_ConFolioInvalido_FallaEnPropiedadFolio(string? folio)
    {
        var validator = new ObtenerSolicitudParaEdicionValidator();
        var result = validator.Validate(new ObtenerSolicitudParaEdicionQuery(folio));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ObtenerSolicitudParaEdicionQuery.Folio));
    }

    [Fact]
    public async Task Handle_Encontrada_NormalizaFolioYMapeaDtoConHorarioCdmxYVersion()
    {
        // 2026-05-21 03:30 UTC == 2026-05-20 21:30 CDMX (sigue siendo día 20 en México, no vencida)
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 5, 21, 3, 30, 0, TimeSpan.Zero));
        var entidad = Solicitud.Crear(
            "0012-AbC/2026",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 5),
            AreaId,
            "Solicitud de auditoría",
            MedioEntrega.PortalWeb,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Link,
            true,
            TipoAcuse.Electronico,
            new DateOnly(2026, 5, 20),
            "admin-01",
            new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc));

        const string version = "0123456789abcdef0123456789abcdef";
        var repo = new StubReadRepository(_ => LecturaSolicitudEdicionOutcome.Encontrada(entidad, version));
        var handler = new ObtenerSolicitudParaEdicionHandler(repo, CrearMapper(), timeProvider);

        var result = await handler.Handle(new ObtenerSolicitudParaEdicionQuery("  0012-abc/2026  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("0012-ABC/2026", repo.UltimoFolioNormalizado);
        Assert.Equal(version, result.Value.VersionConcurrencia);
        Assert.Equal(entidad.Id, result.Value.Solicitud.Id);
        Assert.Equal("0012-AbC/2026", result.Value.Solicitud.Folio);
        Assert.Equal(new TimeOnly(9, 5), result.Value.Solicitud.HoraEntrega);
        Assert.Equal("PortalWeb", result.Value.Solicitud.MedioEntrega);
        Assert.Equal("EnProceso", result.Value.Solicitud.EstatusEntrega);
        Assert.Equal("Informacion", result.Value.Solicitud.TipoSolicitud);
        Assert.Equal("Link", result.Value.Solicitud.TipoEvidencia);
        Assert.True(result.Value.Solicitud.TieneAcuse);
        Assert.Equal("Electronico", result.Value.Solicitud.TipoAcuse);
        Assert.Equal(0, result.Value.Solicitud.DiasRestantes);
        Assert.Equal("EnProceso", result.Value.Solicitud.EstatusEfectivo);
    }

    [Fact]
    public async Task Handle_NoEncontradaYPersistenciaNoDisponible_Mapean404Y503()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        var mapper = CrearMapper();

        var notFound = await new ObtenerSolicitudParaEdicionHandler(
            new StubReadRepository(_ => LecturaSolicitudEdicionOutcome.NoEncontrada()),
            mapper,
            timeProvider).Handle(new ObtenerSolicitudParaEdicionQuery("SOL-404"), CancellationToken.None);

        Assert.True(notFound.IsFailure);
        Assert.Equal(ErrorType.NotFound, notFound.Error.Type);
        Assert.Equal("Solicitud.NoEncontrada", notFound.Error.Code);

        var unavailable = await new ObtenerSolicitudParaEdicionHandler(
            new StubReadRepository(_ => LecturaSolicitudEdicionOutcome.PersistenciaNoDisponible()),
            mapper,
            timeProvider).Handle(new ObtenerSolicitudParaEdicionQuery("SOL-503"), CancellationToken.None);

        Assert.True(unavailable.IsFailure);
        Assert.Equal(ErrorType.Failure, unavailable.Error.Type);
        Assert.Equal("Persistence.Unavailable", unavailable.Error.Code);
    }
}
