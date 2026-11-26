using MapsterMapper;
using VyTSystems.Application;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Domain.Entities;

namespace VyTSystems.UnitTests.Application;

public sealed class ObtenerCatalogosSolicitudHandlerTests
{
    private sealed class StubAreaRepository(LecturaAreasReceptorasResult result) : IAreaReceptoraReadRepository
    {
        public Task<LecturaAreasReceptorasResult> ListarActivasAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task Handle_CuandoDisponible_DevuelveAreasYMappingsExcluyendoVencida()
    {
        var area1 = AreaReceptora.Crear("Dirección de Obras Públicas", 1, true, Guid.Parse("01950000-0000-7000-8000-000000000001"));
        var area2 = AreaReceptora.Crear("Finanzas", 2, true, Guid.Parse("01950000-0000-7000-8000-000000000002"));
        var mapper = new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));
        var handler = new ObtenerCatalogosSolicitudHandler(
            new StubAreaRepository(new LecturaAreasReceptorasResult(true, [area1, area2])),
            mapper);

        var result = await handler.Handle(new ObtenerCatalogosSolicitudQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.AreasReceptoras.Count);
        Assert.Equal(area1.Id, result.Value.AreasReceptoras[0].Id);
        Assert.Equal("Dirección de Obras Públicas", result.Value.AreasReceptoras[0].Nombre);

        Assert.Equal(["Digital", "Impreso", "PortalWeb", "CorreoElectronico"],
            result.Value.MediosEntrega.Select(x => x.Codigo));
        Assert.Equal(["EnProceso", "Entregada"],
            result.Value.EstatusEntrega.Select(x => x.Codigo));
        Assert.DoesNotContain(result.Value.EstatusEntrega, x => x.Codigo == "Vencida");
        Assert.Equal(["Informacion", "Aclaracion", "Otro"],
            result.Value.TiposSolicitud.Select(x => x.Codigo));
        Assert.Equal(["Carpeta", "Link"],
            result.Value.TiposEvidencia.Select(x => x.Codigo));
        Assert.Equal(["Sello", "Folio", "Electronico", "NoAplica"],
            result.Value.TiposAcuse.Select(x => x.Codigo));
    }

    [Fact]
    public async Task Handle_CuandoIndisponible_RetornaPersistenceUnavailable()
    {
        var mapper = new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));
        var handler = new ObtenerCatalogosSolicitudHandler(
            new StubAreaRepository(new LecturaAreasReceptorasResult(false, [])),
            mapper);

        var result = await handler.Handle(new ObtenerCatalogosSolicitudQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.Error.Type);
        Assert.Equal("Persistence.Unavailable", result.Error.Code);
    }
}
