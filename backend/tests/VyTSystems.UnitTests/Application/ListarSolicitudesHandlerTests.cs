using MapsterMapper;
using VyTSystems.Application;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Mapping;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Listar;
using VyTSystems.Domain.Enums;

namespace VyTSystems.UnitTests.Application;

public sealed class ListarSolicitudesHandlerTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingListadoReadRepository(
        Func<FiltroSolicitudesListado, LecturaSolicitudesListadoOutcome> responder)
        : ISolicitudListadoReadRepository
    {
        public FiltroSolicitudesListado? UltimoFiltro { get; private set; }

        public Task<LecturaSolicitudesListadoOutcome> ListarAsync(
            FiltroSolicitudesListado filtro,
            CancellationToken cancellationToken = default)
        {
            UltimoFiltro = filtro;
            return Task.FromResult(responder(filtro));
        }
    }

    private static IMapper CrearMapper() =>
        new Mapper(MappingConfig.CreateConfiguration(typeof(DependencyInjection).Assembly));

    [Fact]
    public async Task Handle_CalculaHoyMexicoEstatusEfectivoDiasRestantesYTotalPagesExactos()
    {
        // 2026-05-16 03:30 UTC == 2026-05-15 21:30 CDMX -> hoyMexico = 2026-05-15
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 16, 3, 30, 0, TimeSpan.Zero));
        var proyecciones = new[]
        {
            // Vence ayer (2026-05-14), EnProceso -> Vencida, DiasRestantes = 0
            new SolicitudListadoProyeccion(
                Guid.Parse("01950000-0000-7000-8000-000000000011"),
                "SOL-2026-001",
                "Asunto vencido",
                "Finanzas",
                MedioEntrega.Digital,
                EstatusEntrega.EnProceso,
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 14),
                "11111111111111111111111111111111"),
            // Vence HOY (2026-05-15), EnProceso -> EnProceso (NO Vencida), DiasRestantes = 0
            new SolicitudListadoProyeccion(
                Guid.Parse("01950000-0000-7000-8000-000000000012"),
                "SOL-2026-002",
                "Asunto vence hoy",
                "Compras",
                MedioEntrega.PortalWeb,
                EstatusEntrega.EnProceso,
                new DateOnly(2026, 5, 10),
                new DateOnly(2026, 5, 15),
                "22222222222222222222222222222222"),
            // Vence pasado pero Entregada -> Entregada, DiasRestantes = 0
            new SolicitudListadoProyeccion(
                Guid.Parse("01950000-0000-7000-8000-000000000013"),
                "SOL-2026-003",
                "Asunto entregado",
                "Jurídico",
                MedioEntrega.CorreoElectronico,
                EstatusEntrega.Entregada,
                new DateOnly(2026, 5, 2),
                new DateOnly(2026, 5, 10),
                "33333333333333333333333333333333"),
            // Vence en 5 días (2026-05-20), EnProceso -> EnProceso, DiasRestantes = 5
            new SolicitudListadoProyeccion(
                Guid.Parse("01950000-0000-7000-8000-000000000014"),
                "SOL-2026-004",
                "Asunto futuro",
                "Auditoría interna",
                MedioEntrega.Impreso,
                EstatusEntrega.EnProceso,
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 20),
                "44444444444444444444444444444444")
        };

        var repo = new CapturingListadoReadRepository(_ =>
            LecturaSolicitudesListadoOutcome.Consultado(proyecciones, totalItems: 21));

        var handler = new ListarSolicitudesHandler(repo, CrearMapper(), clock);
        var result = await handler.Handle(new ListarSolicitudesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 5, 15), repo.UltimoFiltro!.HoyMexico);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(21, result.Value.TotalItems);
        Assert.Equal(3, result.Value.TotalPages);

        Assert.Equal("Vencida", result.Value.Items[0].EstatusEfectivo);
        Assert.Equal(0, result.Value.Items[0].DiasRestantes);

        Assert.Equal("EnProceso", result.Value.Items[1].EstatusEfectivo);
        Assert.Equal(0, result.Value.Items[1].DiasRestantes);

        Assert.Equal("Entregada", result.Value.Items[2].EstatusEfectivo);
        Assert.Equal(0, result.Value.Items[2].DiasRestantes);

        Assert.Equal("EnProceso", result.Value.Items[3].EstatusEfectivo);
        Assert.Equal(5, result.Value.Items[3].DiasRestantes);
        Assert.Equal("44444444444444444444444444444444", result.Value.Items[3].VersionConcurrencia);
    }

    [Fact]
    public async Task Handle_TraduceTodosLosModosDeFiltroDiasYBusquedaFolioOFecha()
    {
        var hoy = new DateOnly(2026, 5, 15);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var mapper = CrearMapper();
        var repo = new CapturingListadoReadRepository(_ =>
            LecturaSolicitudesListadoOutcome.Consultado([], 0));
        var handler = new ListarSolicitudesHandler(repo, mapper, clock);

        // 1. Fecha exacta dd/MM/yyyy
        var resVacio = await handler.Handle(new ListarSolicitudesQuery(Q: "20/05/2026"), CancellationToken.None);
        Assert.Equal(0, resVacio.Value.TotalPages);
        Assert.Equal(new DateOnly(2026, 5, 20), repo.UltimoFiltro!.FechaBusquedaExacta);
        Assert.Null(repo.UltimoFiltro.FolioParcialNormalizado);

        // 2. Folio parcial con caracteres no alfanuméricos aislados ("_a%" y "_")
        await handler.Handle(new ListarSolicitudesQuery(Q: "  _ab%  "), CancellationToken.None);
        Assert.Equal("_AB%", repo.UltimoFiltro.FolioParcialNormalizado);
        Assert.Null(repo.UltimoFiltro.FechaBusquedaExacta);

        await handler.Handle(new ListarSolicitudesQuery(Q: "0045-abc/2026"), CancellationToken.None);
        Assert.Equal("0045-ABC/2026", repo.UltimoFiltro.FolioParcialNormalizado);

        // 3. dias = "0" (estrictamente menor que hoyMexico)
        await handler.Handle(new ListarSolicitudesQuery(Dias: "0"), CancellationToken.None);
        Assert.True(repo.UltimoFiltro.SoloNoEntregadas);
        Assert.Equal(hoy, repo.UltimoFiltro.VencimientoEstrictoMenorQue);
        Assert.Null(repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Null(repo.UltimoFiltro.DiasVencimientoHasta);

        // 4. dias = "1-3", "4-7", "1-7", "8+"
        await handler.Handle(new ListarSolicitudesQuery(Dias: "1-3"), CancellationToken.None);
        Assert.Equal(hoy.AddDays(1), repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Equal(hoy.AddDays(3), repo.UltimoFiltro.DiasVencimientoHasta);

        await handler.Handle(new ListarSolicitudesQuery(Dias: "4-7"), CancellationToken.None);
        Assert.Equal(hoy.AddDays(4), repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Equal(hoy.AddDays(7), repo.UltimoFiltro.DiasVencimientoHasta);

        await handler.Handle(new ListarSolicitudesQuery(Dias: "1-7"), CancellationToken.None);
        Assert.Equal(hoy.AddDays(1), repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Equal(hoy.AddDays(7), repo.UltimoFiltro.DiasVencimientoHasta);

        await handler.Handle(new ListarSolicitudesQuery(Dias: "8+"), CancellationToken.None);
        Assert.Equal(hoy.AddDays(8), repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Null(repo.UltimoFiltro.DiasVencimientoHasta);

        // 5. dias = "custom" con min=0 (incluye vencidas y hoy hasta max) vs min>=1
        await handler.Handle(new ListarSolicitudesQuery(Dias: "custom", DiasMax: "5"), CancellationToken.None);
        Assert.True(repo.UltimoFiltro.SoloNoEntregadas);
        Assert.Null(repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Equal(hoy.AddDays(5), repo.UltimoFiltro.DiasVencimientoHasta);

        await handler.Handle(new ListarSolicitudesQuery(Dias: "custom", DiasMin: "2"), CancellationToken.None);
        Assert.True(repo.UltimoFiltro.SoloNoEntregadas);
        Assert.Equal(hoy.AddDays(2), repo.UltimoFiltro.DiasVencimientoDesde);
        Assert.Equal(hoy.AddDays(999), repo.UltimoFiltro.DiasVencimientoHasta);
    }

    [Fact]
    public async Task Handle_PersistenciaNoDisponible_RetornaFailureUnavailable()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var repo = new CapturingListadoReadRepository(_ =>
            LecturaSolicitudesListadoOutcome.PersistenciaNoDisponible());

        var result = await new ListarSolicitudesHandler(repo, CrearMapper(), clock)
            .Handle(new ListarSolicitudesQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.Error.Type);
        Assert.Equal("Persistence.Unavailable", result.Error.Code);
    }
}
