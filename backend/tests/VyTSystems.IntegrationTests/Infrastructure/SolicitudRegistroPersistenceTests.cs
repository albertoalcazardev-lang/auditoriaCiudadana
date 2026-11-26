using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;
using VyTSystems.Infrastructure;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Infrastructure;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class SolicitudRegistroPersistenceTests(MySqlTestFixture fixture)
{
    private static readonly Guid AreaIdActiva = Guid.Parse("01950000-0000-7000-8000-000000000001");

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

    private Solicitud ConstruirSolicitud(
        string folio,
        bool esAutomatico,
        string? usuarioId = null,
        Guid? areaId = null) =>
        Solicitud.Crear(
            folio,
            new DateOnly(2026, 5, 10),
            new TimeOnly(11, 20),
            areaId ?? AreaIdActiva,
            "Solicitud de prueba de integración",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 25),
            usuarioId ?? fixture.AdminUserId,
            new DateTime(2026, 5, 10, 17, 20, 0, DateTimeKind.Utc),
            esAutomatico);

    private async Task<int?> ObtenerUltimoNumeroSecuenciaAsync(int anio)
    {
        await using var db = fixture.CreateDbContext();
        var lista = await db.Database
            .SqlQueryRaw<int>("SELECT ultimo_numero AS Value FROM folio_secuencias WHERE anio = {0}", anio)
            .ToListAsync();
        return lista.Count == 0 ? null : lista[0];
    }

    [Fact]
    public async Task FolioManual_PreservaCerosYCasingOriginal_YRechazaDuplicadoCaseInsensitive()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using var scope1 = provider.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var res1 = await repo1.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-manual-1",
                new string('1', 64),
                false,
                "00045-AbC/2026",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false));

        Assert.Equal(RegistroSolicitudEstado.Creada, res1.Estado);
        Assert.Equal("00045-AbC/2026", res1.Solicitud!.Folio);
        Assert.Equal("00045-ABC/2026", res1.Solicitud.FolioNormalizado);

        using var scope2 = provider.CreateScope();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var res2 = await repo2.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-manual-2",
                new string('2', 64),
                false,
                "00045-abc/2026",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false));

        Assert.Equal(RegistroSolicitudEstado.FolioDuplicado, res2.Estado);
    }

    [Fact]
    public async Task FoliosConDistintaCantidadDeCerosIniciales_CoexistenEnBaseReal()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using var scope1 = provider.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var res1 = await repo1.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-ceros-1",
                new string('1', 64),
                false,
                "SOL-2026-01",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false));

        using var scope2 = provider.CreateScope();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var res2 = await repo2.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-ceros-2",
                new string('2', 64),
                false,
                "SOL-2026-001",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false));

        Assert.Equal(RegistroSolicitudEstado.Creada, res1.Estado);
        Assert.Equal("SOL-2026-01", res1.Solicitud!.Folio);
        Assert.Equal(RegistroSolicitudEstado.Creada, res2.Estado);
        Assert.Equal("SOL-2026-001", res2.Solicitud!.Folio);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Solicitudes.CountAsync());
    }

    [Fact]
    public async Task IdempotencyKey_AisladaPorUsuario_PermiteMismaClaveEnDosUsuariosDistintos()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        const string claveCompartida = "shared-key-per-user";

        using var scopeAdmin = provider.CreateScope();
        var repoAdmin = scopeAdmin.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var resAdmin = await repoAdmin.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                claveCompartida,
                new string('a', 64),
                false,
                "USR-ADMIN-01",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false, fixture.AdminUserId));

        using var scopeConsulta = provider.CreateScope();
        var repoConsulta = scopeConsulta.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var resConsulta = await repoConsulta.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.ConsultaUserId,
                claveCompartida,
                new string('b', 64),
                false,
                "USR-CONS-01",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false, fixture.ConsultaUserId));

        Assert.Equal(RegistroSolicitudEstado.Creada, resAdmin.Estado);
        Assert.Equal(RegistroSolicitudEstado.Creada, resConsulta.Estado);
        Assert.NotEqual(resAdmin.Solicitud!.Id, resConsulta.Solicitud!.Id);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Solicitudes.CountAsync());
    }

    [Fact]
    public async Task IdempotencyKey_ReutilizableTrasRechazoPorFolioDuplicado()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using (var scope0 = provider.CreateScope())
        {
            var repo = scope0.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var semilla = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "key-inicial",
                    new string('0', 64),
                    false,
                    "FOLIO-OCUPADO-01",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, false));
            Assert.Equal(RegistroSolicitudEstado.Creada, semilla.Estado);
        }

        const string claveReutilizable = "key-reutilizable-tras-duplicado";

        using (var scopeDup = provider.CreateScope())
        {
            var repo = scopeDup.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var rechazo = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    claveReutilizable,
                    new string('1', 64),
                    false,
                    "folio-ocupado-01",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, false));
            Assert.Equal(RegistroSolicitudEstado.FolioDuplicado, rechazo.Estado);
        }

        using (var scopeOk = provider.CreateScope())
        {
            var repo = scopeOk.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var exito = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    claveReutilizable,
                    new string('2', 64),
                    false,
                    "FOLIO-NUEVO-02",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, false));
            Assert.Equal(RegistroSolicitudEstado.Creada, exito.Estado);
            Assert.Equal("FOLIO-NUEVO-02", exito.Solicitud!.Folio);
        }
    }

    [Fact]
    public async Task Concurrencia_DosSolicitudesManualesConMismoFolio_SoloUnaGana()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using var barrier = new Barrier(2);
        var tasks = Enumerable.Range(1, 2).Select(i => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            barrier.SignalAndWait();
            return await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    $"key-conc-manual-{i}",
                    new string((char)('a' + i), 64),
                    false,
                    "CONC-2026-001",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, false));
        })).ToArray();

        var resultados = await Task.WhenAll(tasks);

        Assert.Single(resultados, r => r.Estado == RegistroSolicitudEstado.Creada);
        Assert.Single(resultados, r => r.Estado == RegistroSolicitudEstado.FolioDuplicado);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Solicitudes.CountAsync());
    }

    [Fact]
    public async Task Concurrencia_GeneracionAutomatica15Paralelas_GeneraFoliosUnicosConsecutivos()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        const int total = 15;
        using var barrier = new Barrier(total);
        var tasks = Enumerable.Range(1, total).Select(i => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            barrier.SignalAndWait();
            return await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    $"key-auto-{i}",
                    $"{i:D64}",
                    true,
                    null,
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, true));
        })).ToArray();

        var resultados = await Task.WhenAll(tasks);

        Assert.All(resultados, r => Assert.Equal(RegistroSolicitudEstado.Creada, r.Estado));

        var folios = resultados.Select(r => r.Solicitud!.Folio).OrderBy(f => f).ToArray();
        var esperados = Enumerable.Range(1, total)
            .Select(n => Solicitud.FormatearFolioAutomatico(2026, n))
            .OrderBy(f => f)
            .ToArray();

        Assert.Equal(esperados, folios);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(total, await db.Solicitudes.CountAsync());
        Assert.Equal(total, await ObtenerUltimoNumeroSecuenciaAsync(2026));
    }

    [Fact]
    public async Task ColisionYSalto_EntreFolioManualYAutomatico_SaltaFoliosOcupados()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using (var scopeManual = provider.CreateScope())
        {
            var repo = scopeManual.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var m1 = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "manual-sol-1",
                    new string('1', 64),
                    false,
                    "sol-2026-001",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, false));
            Assert.Equal(RegistroSolicitudEstado.Creada, m1.Estado);
        }

        using (var scopeAuto = provider.CreateScope())
        {
            var repo = scopeAuto.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var auto = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "auto-salto-1",
                    new string('2', 64),
                    true,
                    null,
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, true));

            Assert.Equal(RegistroSolicitudEstado.Creada, auto.Estado);
            Assert.Equal("SOL-2026-002", auto.Solicitud!.Folio);
        }

        Assert.Equal(2, await ObtenerUltimoNumeroSecuenciaAsync(2026));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrencia_MismaIdempotencyKey_SoloUnaFilaYSinAvanceExtraDeSecuencia(bool automatico)
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        const int hilos = 8;
        using var barrier = new Barrier(hilos);
        var key = automatico ? "idem-auto-conc" : "idem-manual-conc";
        var hash = new string(automatico ? 'a' : 'm', 64);

        var tasks = Enumerable.Range(1, hilos).Select(_ => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            barrier.SignalAndWait();
            return await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    key,
                    hash,
                    automatico,
                    automatico ? null : "MANUAL-IDEM-001",
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, automatico));
        })).ToArray();

        var resultados = await Task.WhenAll(tasks);

        Assert.Single(resultados, r => r.Estado == RegistroSolicitudEstado.Creada);
        Assert.Equal(hilos - 1, resultados.Count(r => r.Estado == RegistroSolicitudEstado.RecuperadaPorIdempotencia));

        var idsDistintos = resultados.Select(r => r.Solicitud!.Id).Distinct().ToArray();
        Assert.Single(idsDistintos);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Solicitudes.CountAsync());
        if (automatico)
        {
            Assert.Equal(1, await ObtenerUltimoNumeroSecuenciaAsync(2026));
            Assert.Equal("SOL-2026-001", resultados[0].Solicitud!.Folio);
        }
    }

    [Fact]
    public async Task Idempotencia_ReintentoTrasPerdidaDeRespuesta_YConflictoPayloadDiferente()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        Guid idOriginal;
        string folioOriginal;
        using (var scope1 = provider.CreateScope())
        {
            var repo = scope1.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var r1 = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "key-replay-1",
                    new string('a', 64),
                    true,
                    null,
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, true));
            Assert.Equal(RegistroSolicitudEstado.Creada, r1.Estado);
            idOriginal = r1.Solicitud!.Id;
            folioOriginal = r1.Solicitud.Folio;
        }

        using (var scope2 = provider.CreateScope())
        {
            var repo = scope2.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var replay = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "key-replay-1",
                    new string('a', 64),
                    true,
                    null,
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, true));
            Assert.Equal(RegistroSolicitudEstado.RecuperadaPorIdempotencia, replay.Estado);
            Assert.Equal(idOriginal, replay.Solicitud!.Id);
            Assert.Equal(folioOriginal, replay.Solicitud.Folio);
            Assert.Equal(DateTimeKind.Utc, replay.Solicitud.CreadoEnUtc.Kind);
            Assert.Equal(new TimeOnly(11, 20), replay.Solicitud.HoraEntrega);
            Assert.Equal(new DateOnly(2026, 5, 25), replay.Solicitud.FechaVencimiento);
        }

        using (var scope3 = provider.CreateScope())
        {
            var repo = scope3.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var conflicto = await repo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "key-replay-1",
                    new string('b', 64),
                    true,
                    null,
                    AreaIdActiva,
                    2026),
                folio => ConstruirSolicitud(folio, true));
            Assert.Equal(RegistroSolicitudEstado.IdempotenciaConflicto, conflicto.Estado);
        }
    }

    [Fact]
    public async Task Rollback_SiFallaInsercionDespuesDeSecuencia_NoDejaRegistrosParciales()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();

        // Usuario inexistente provocará violación de FK 1452 al hacer SaveChanges, tras avanzar secuencia en tx.
        var outcome = await repo.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-rollback-fk",
                new string('z', 64),
                true,
                null,
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, true, usuarioId: "usuario-inexistente-fk"));

        Assert.Equal(RegistroSolicitudEstado.PersistenciaNoDisponible, outcome.Estado);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(0, await db.Solicitudes.CountAsync());
        Assert.Null(await ObtenerUltimoNumeroSecuenciaAsync(2026));
    }

    [Fact]
    public async Task AreaInactiva_NoSeListaEnCatalogo_YRechazaRegistro()
    {
        await fixture.ResetSolicitudesAsync();
        var areaInactivaId = Guid.Parse("01950000-0000-7000-8000-000000000098");

        await using (var db = fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO areas_receptoras (id, nombre, activa, orden)
                VALUES ({areaInactivaId}, 'Área Histórica Inactiva', 0, 99)
                ON DUPLICATE KEY UPDATE activa = 0;");
        }

        try
        {
            await using var provider = BuildProvider();
            using var scope = provider.CreateScope();

            var areaRepo = scope.ServiceProvider.GetRequiredService<IAreaReceptoraReadRepository>();
            var lectura = await areaRepo.ListarActivasAsync();
            Assert.True(lectura.Disponible);
            Assert.DoesNotContain(lectura.Areas, a => a.Id == areaInactivaId);

            var solicitudRepo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var outcome = await solicitudRepo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    "key-area-inactiva",
                    new string('q', 64),
                    false,
                    "INACT-001",
                    areaInactivaId,
                    2026),
                folio => Solicitud.Crear(
                    folio,
                    new DateOnly(2026, 5, 10),
                    new TimeOnly(11, 20),
                    areaInactivaId,
                    "Prueba área inactiva",
                    MedioEntrega.Digital,
                    EstatusEntrega.EnProceso,
                    TipoSolicitud.Informacion,
                    TipoEvidencia.Carpeta,
                    false,
                    TipoAcuse.NoAplica,
                    new DateOnly(2026, 5, 20),
                    fixture.AdminUserId,
                    new DateTime(2026, 5, 10, 17, 20, 0, DateTimeKind.Utc),
                    false));

            Assert.Equal(RegistroSolicitudEstado.AreaReceptoraInvalida, outcome.Estado);
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            await db.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM areas_receptoras WHERE id = {areaInactivaId};");
        }
    }

    [Fact]
    public async Task FallaDeConexion_RetornaUnavailableSinLanzarExcepcion()
    {
        var invalidPwd = Guid.NewGuid().ToString("N");
        var closedPortConn = $"Server=127.0.0.1;Port=39999;Database=no_existe;Uid=u;Pwd={invalidPwd};Connection Timeout=2;";
        await using var provider = BuildProvider(closedPortConn);

        using var scope = provider.CreateScope();
        var areaRepo = scope.ServiceProvider.GetRequiredService<IAreaReceptoraReadRepository>();
        var areas = await areaRepo.ListarActivasAsync();
        Assert.False(areas.Disponible);
        Assert.Empty(areas.Areas);

        var solicitudRepo = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var outcome = await solicitudRepo.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                "key-offline",
                new string('x', 64),
                false,
                "OFF-001",
                AreaIdActiva,
                2026),
            folio => ConstruirSolicitud(folio, false));
        Assert.Equal(RegistroSolicitudEstado.PersistenciaNoDisponible, outcome.Estado);
    }
}
