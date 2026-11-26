using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;
using VyTSystems.Infrastructure;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Infrastructure;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class DashboardReadRepositoryTests(MySqlTestFixture fixture)
{
    private static readonly Guid[] AreasCatalogo =
    [
        Guid.Parse("01950000-0000-7000-8000-000000000001"), // Dirección de Obras Públicas
        Guid.Parse("01950000-0000-7000-8000-000000000002"), // Finanzas
        Guid.Parse("01950000-0000-7000-8000-000000000003"), // Compras
        Guid.Parse("01950000-0000-7000-8000-000000000004"), // Auditoría interna
        Guid.Parse("01950000-0000-7000-8000-000000000005"), // Tecnologías de la Información
        Guid.Parse("01950000-0000-7000-8000-000000000006"), // Recursos Humanos
        Guid.Parse("01950000-0000-7000-8000-000000000007"), // Jurídico
        Guid.Parse("01950000-0000-7000-8000-000000000008")  // Comisión Estatal del Agua
    ];

    private ServiceProvider BuildProvider(string? connectionString = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString ?? fixture.ConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private Solicitud CrearSemilla(
        string folio,
        DateOnly fechaEntrega,
        DateOnly fechaVencimiento,
        EstatusEntrega estatusEntrega,
        Guid areaReceptoraId,
        DateTime creadoEnUtc,
        string asunto = "Asunto de prueba dashboard") =>
        Solicitud.Crear(
            folio,
            fechaEntrega,
            new TimeOnly(10, 30),
            areaReceptoraId,
            asunto,
            MedioEntrega.Digital,
            estatusEntrega,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            false,
            TipoAcuse.NoAplica,
            fechaVencimiento,
            fixture.AdminUserId,
            DateTime.SpecifyKind(creadoEnUtc, DateTimeKind.Utc),
            false);

    private async Task InsertarSolicitudesAsync(params Solicitud[] solicitudes)
    {
        await using var db = fixture.CreateDbContext();
        foreach (var solicitud in solicitudes)
        {
            var entry = db.Solicitudes.Add(solicitud);
            entry.Property("IdempotencyKey").CurrentValue = $"idem-{Guid.NewGuid():N}";
            entry.Property("IdempotencyPayloadHash").CurrentValue = new string('d', 64);
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ObtenerResumenAsync_TablaVaciaYPeriodoSinRegistros_RetornaCerosYColeccionesVaciasSinInvalidCast()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();
        var hoy = new DateOnly(2026, 5, 15);

        // 1. Tabla completamente vacía
        using (var scope1 = provider.CreateScope())
        {
            var repo = scope1.ServiceProvider.GetRequiredService<IDashboardReadRepository>();
            var outcomeVacio = await repo.ObtenerResumenAsync(
                new ConsultaDashboardParametros(
                    hoy,
                    new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
                    new RangoFechasDashboard(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30))));

            Assert.Equal(LecturaDashboardEstado.Completada, outcomeVacio.Estado);
            var datos = outcomeVacio.Datos!;
            Assert.Equal(0, datos.Total);
            Assert.Equal(0, datos.EnProceso);
            Assert.Equal(0, datos.Entregadas);
            Assert.Equal(0, datos.Vencidas);
            Assert.Equal(0, datos.ProximasAVencer);
            Assert.Equal(0, datos.TotalPeriodoAnterior);
            Assert.Empty(datos.PorArea);
            Assert.Empty(datos.ProximosVencimientos);
            Assert.Empty(datos.ActividadReciente);
        }

        // 2. Registros en abril (periodo anterior), pero ninguno en mayo (periodo actual)
        await InsertarSolicitudesAsync(
            CrearSemilla(
                "ABR-2026-001",
                new DateOnly(2026, 4, 10),
                new DateOnly(2026, 4, 20),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc)),
            CrearSemilla(
                "ABR-2026-002",
                new DateOnly(2026, 4, 18),
                new DateOnly(2026, 4, 25),
                EstatusEntrega.EnProceso,
                AreasCatalogo[1],
                new DateTime(2026, 4, 18, 16, 0, 0, DateTimeKind.Utc)));

        using (var scope2 = provider.CreateScope())
        {
            var repo = scope2.ServiceProvider.GetRequiredService<IDashboardReadRepository>();
            var outcomeSinActual = await repo.ObtenerResumenAsync(
                new ConsultaDashboardParametros(
                    hoy,
                    new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
                    new RangoFechasDashboard(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30))));

            Assert.Equal(LecturaDashboardEstado.Completada, outcomeSinActual.Estado);
            var datos = outcomeSinActual.Datos!;
            Assert.Equal(0, datos.Total);
            Assert.Equal(0, datos.EnProceso);
            Assert.Equal(0, datos.Entregadas);
            Assert.Equal(0, datos.Vencidas);
            Assert.Equal(0, datos.ProximasAVencer);
            Assert.Equal(2, datos.TotalPeriodoAnterior);
            Assert.Empty(datos.PorArea);
            Assert.Empty(datos.ProximosVencimientos);
            Assert.Empty(datos.ActividadReciente);
        }
    }

    [Fact]
    public async Task ObtenerResumenAsync_ReconciliacionClasificacionEfectivaVenceHoyYProximosVencimientos()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();
        var hoy = new DateOnly(2026, 5, 15);

        await InsertarSolicitudesAsync(
            // 1. Entregada con fecha de vencimiento PASADA -> Entregada (NO Vencida)
            CrearSemilla(
                "ENT-PASADA-01",
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 5),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
                "Entregada vencimiento pasado"),
            // 2. Entregada con vencimiento en 2 días -> Entregada (NO en ProximasAVencer ni listado)
            CrearSemilla(
                "ENT-PROX-02",
                new DateOnly(2026, 5, 10),
                new DateOnly(2026, 5, 17),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
                "Entregada vencimiento cercano"),
            // 3. No entregada que vence HOY (0 días restantes) -> EnProceso (NO Vencida y NO ProximasAVencer)
            CrearSemilla(
                "HOY-00-DIAS",
                new DateOnly(2026, 5, 5),
                hoy,
                EstatusEntrega.EnProceso,
                AreasCatalogo[1],
                new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc),
                "Vence exactamente hoy"),
            // 4. No entregada vencida ayer -> Vencida
            CrearSemilla(
                "VENC-AYER-01",
                new DateOnly(2026, 5, 2),
                new DateOnly(2026, 5, 14),
                EstatusEntrega.EnProceso,
                AreasCatalogo[1],
                new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc),
                "Venció ayer"),
            // 5..10. Seis solicitudes próximas a vencer (entre +1 y +7 días) para probar conteo total (6) y top 5 ordenado
            CrearSemilla(
                "PROX-1D-B",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 16), // +1 día
                EstatusEntrega.EnProceso,
                AreasCatalogo[2],
                new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc),
                "Vence mañana B"),
            CrearSemilla(
                "PROX-1D-a",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 16), // +1 día (desempata antes de PROX-1D-B por FolioNormalizado)
                EstatusEntrega.EnProceso,
                AreasCatalogo[2],
                new DateTime(2026, 5, 12, 11, 0, 0, DateTimeKind.Utc),
                "Vence mañana A"),
            CrearSemilla(
                "PROX-3D",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 18), // +3 días
                EstatusEntrega.EnProceso,
                AreasCatalogo[2],
                new DateTime(2026, 5, 12, 12, 0, 0, DateTimeKind.Utc),
                "Vence en 3 días"),
            CrearSemilla(
                "PROX-5D",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 20), // +5 días
                EstatusEntrega.EnProceso,
                AreasCatalogo[3],
                new DateTime(2026, 5, 12, 13, 0, 0, DateTimeKind.Utc),
                "Vence en 5 días"),
            CrearSemilla(
                "PROX-6D",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 21), // +6 días
                EstatusEntrega.EnProceso,
                AreasCatalogo[3],
                new DateTime(2026, 5, 12, 14, 0, 0, DateTimeKind.Utc),
                "Vence en 6 días"),
            CrearSemilla(
                "PROX-7D-LIMITE",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 22), // +7 días (incluida en conteo ProximasAVencer, fuera de top 5)
                EstatusEntrega.EnProceso,
                AreasCatalogo[3],
                new DateTime(2026, 5, 12, 15, 0, 0, DateTimeKind.Utc),
                "Vence en 7 días"),
            // 11. No entregada que vence en +8 días -> EnProceso pero NO ProximasAVencer
            CrearSemilla(
                "FUT-8D-FUERA",
                new DateOnly(2026, 5, 13),
                new DateOnly(2026, 5, 23), // +8 días
                EstatusEntrega.EnProceso,
                AreasCatalogo[4],
                new DateTime(2026, 5, 13, 16, 0, 0, DateTimeKind.Utc),
                "Vence en 8 días"));

        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDashboardReadRepository>();

        var outcome = await repo.ObtenerResumenAsync(
            new ConsultaDashboardParametros(
                hoy,
                new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
                null));

        Assert.Equal(LecturaDashboardEstado.Completada, outcome.Estado);
        var datos = outcome.Datos!;

        // Entregadas: 2 | Vencidas: 1 | EnProceso: 1 (hoy) + 6 (1..7d) + 1 (8d) = 8 | Total = 11
        Assert.Equal(2, datos.Entregadas);
        Assert.Equal(1, datos.Vencidas);
        Assert.Equal(8, datos.EnProceso);
        Assert.Equal(11, datos.Total);
        Assert.Equal(datos.EnProceso + datos.Entregadas + datos.Vencidas, datos.Total);

        // ProximasAVencer es subconjunto estricto de EnProceso (6 <= 8)
        Assert.Equal(6, datos.ProximasAVencer);
        Assert.True(datos.ProximasAVencer <= datos.EnProceso);
        Assert.Null(datos.TotalPeriodoAnterior);

        // Top 5 próximos vencimientos en orden ascendente y excluyendo entregadas, hoy (0d) y +8d
        Assert.Equal(5, datos.ProximosVencimientos.Count);
        Assert.Equal(
            ["PROX-1D-a", "PROX-1D-B", "PROX-3D", "PROX-5D", "PROX-6D"],
            datos.ProximosVencimientos.Select(x => x.Folio).ToArray());
        Assert.Equal(
            [1, 1, 3, 5, 6],
            datos.ProximosVencimientos.Select(x => x.DiasRestantes).ToArray());

        // Actividad reciente: máximo 5 elementos ordenados descendentemente por CreadoEnUtc real
        Assert.Equal(5, datos.ActividadReciente.Count);
        Assert.Equal(
            ["FUT-8D-FUERA", "PROX-7D-LIMITE", "PROX-6D", "PROX-5D", "PROX-3D"],
            datos.ActividadReciente.Select(x => x.Folio).ToArray());
        Assert.All(datos.ActividadReciente, item =>
        {
            Assert.Equal("NuevaSolicitud", item.Tipo);
            Assert.Equal("Nueva solicitud registrada", item.Descripcion);
            Assert.Equal(DateTimeKind.Utc, item.OcurridoEnUtc.Kind);
        });
    }

    [Fact]
    public async Task ObtenerResumenAsync_ConMasDeSeisAreas_RetornaTopSeisOrdenadasYOtrasReconciliadaConTotal()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();
        var hoy = new DateOnly(2026, 5, 15);

        // Sembramos en 8 áreas distintas con conteos: 5, 4, 3, 2, 2, 2, 1, 1 -> Total = 20
        // Áreas con 2 solicitudes: Compras (idx 2), Auditoría interna (idx 3), Tecnologías de la Información (idx 4)
        // Áreas con 1 solicitud: Jurídico (idx 6) y Comisión Estatal del Agua (idx 7) -> van a "Otras" con suma = 2
        var distribucion = new (Guid AreaId, int Cantidad)[]
        {
            (AreasCatalogo[0], 5), // Dirección de Obras Públicas: 5
            (AreasCatalogo[1], 4), // Finanzas: 4
            (AreasCatalogo[5], 3), // Recursos Humanos: 3
            (AreasCatalogo[2], 2), // Compras: 2
            (AreasCatalogo[3], 2), // Auditoría interna: 2
            (AreasCatalogo[4], 2), // Tecnologías de la Información: 2
            (AreasCatalogo[6], 1), // Jurídico: 1 -> Otras
            (AreasCatalogo[7], 1)  // Comisión Estatal del Agua: 1 -> Otras
        };

        var semillas = new List<Solicitud>();
        var contador = 1;
        foreach (var (areaId, cantidad) in distribucion)
        {
            for (var i = 0; i < cantidad; i++)
            {
                semillas.Add(CrearSemilla(
                    $"AREA-TEST-{contador:D3}",
                    new DateOnly(2026, 5, 10),
                    new DateOnly(2026, 5, 25),
                    EstatusEntrega.EnProceso,
                    areaId,
                    new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc).AddMinutes(contador)));
                contador++;
            }
        }

        await InsertarSolicitudesAsync([.. semillas]);

        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDashboardReadRepository>();
        var outcome = await repo.ObtenerResumenAsync(
            new ConsultaDashboardParametros(
                hoy,
                new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
                null));

        Assert.Equal(LecturaDashboardEstado.Completada, outcome.Estado);
        var datos = outcome.Datos!;

        Assert.Equal(20, datos.Total);
        Assert.Equal(7, datos.PorArea.Count); // 6 principales + "Otras"
        Assert.Equal(datos.Total, datos.PorArea.Sum(x => x.Cantidad));

        Assert.Equal("Dirección de Obras Públicas", datos.PorArea[0].Area);
        Assert.Equal(5, datos.PorArea[0].Cantidad);
        Assert.Equal("Finanzas", datos.PorArea[1].Area);
        Assert.Equal(4, datos.PorArea[1].Cantidad);
        Assert.Equal("Recursos Humanos", datos.PorArea[2].Area);
        Assert.Equal(3, datos.PorArea[2].Cantidad);

        // Desempate alfabético entre las tres áreas con Cantidad == 2
        Assert.Equal("Auditoría interna", datos.PorArea[3].Area);
        Assert.Equal(2, datos.PorArea[3].Cantidad);
        Assert.Equal("Compras", datos.PorArea[4].Area);
        Assert.Equal(2, datos.PorArea[4].Cantidad);
        Assert.Equal("Tecnologías de la Información", datos.PorArea[5].Area);
        Assert.Equal(2, datos.PorArea[5].Cantidad);

        // Agrupación remanente en "Otras"
        Assert.Equal("Otras", datos.PorArea[6].Area);
        Assert.Equal(2, datos.PorArea[6].Cantidad);
    }

    [Fact]
    public async Task ObtenerResumenAsync_FallaDisponibilidadYCancelacion_ClasificaCorrectamente()
    {
        var invalidPwd = Guid.NewGuid().ToString("N");
        var offlineConn = $"Server=127.0.0.1;Port=39997;Database=no_existe;Uid=u;Pwd={invalidPwd};Connection Timeout=1;";
        await using var provider = BuildProvider(offlineConn);

        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDashboardReadRepository>();
        var parametros = new ConsultaDashboardParametros(
            new DateOnly(2026, 5, 15),
            new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
            new RangoFechasDashboard(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));

        var outcomeOffline = await repo.ObtenerResumenAsync(parametros);
        Assert.Equal(LecturaDashboardEstado.PersistenciaNoDisponible, outcomeOffline.Estado);
        Assert.Null(outcomeOffline.Datos);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.ObtenerResumenAsync(parametros, cts.Token));
    }
}
