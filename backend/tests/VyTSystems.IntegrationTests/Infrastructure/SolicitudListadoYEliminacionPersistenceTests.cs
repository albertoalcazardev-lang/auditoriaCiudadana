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
public sealed class SolicitudListadoYEliminacionPersistenceTests(MySqlTestFixture fixture)
{
    private static readonly Guid AreaActiva1 = Guid.Parse("01950000-0000-7000-8000-000000000001");
    private static readonly Guid AreaActiva2 = Guid.Parse("01950000-0000-7000-8000-000000000002");

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

    private async Task<Solicitud> SembrarSolicitudAsync(
        ServiceProvider provider,
        string folio,
        string idempotencyKey,
        DateOnly fechaEntrega,
        DateOnly fechaVencimiento,
        EstatusEntrega estatus = EstatusEntrega.EnProceso,
        bool automatico = false,
        string? usuarioId = null)
    {
        using var scope = provider.CreateScope();
        var registro = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var outcome = await registro.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                usuarioId ?? fixture.AdminUserId,
                idempotencyKey,
                $"{Math.Abs(idempotencyKey.GetHashCode()):D64}"[..64],
                automatico,
                automatico ? null : folio,
                AreaActiva1,
                2026),
            f => Solicitud.Crear(
                f,
                fechaEntrega,
                new TimeOnly(10, 30),
                AreaActiva1,
                $"Asunto {f}",
                MedioEntrega.Digital,
                estatus,
                TipoSolicitud.Informacion,
                TipoEvidencia.Carpeta,
                false,
                TipoAcuse.NoAplica,
                fechaVencimiento,
                usuarioId ?? fixture.AdminUserId,
                new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc),
                automatico));

        Assert.Equal(RegistroSolicitudEstado.Creada, outcome.Estado);
        return outcome.Solicitud!;
    }

    private async Task<string> ObtenerVersionActualAsync(ServiceProvider provider, string folioNormalizado)
    {
        using var scope = provider.CreateScope();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
        var res = await readRepo.ObtenerPorFolioNormalizadoAsync(folioNormalizado);
        Assert.Equal(LecturaSolicitudEdicionEstado.Encontrada, res.Estado);
        return res.VersionConcurrencia!;
    }

    [Fact]
    public async Task ListarAsync_EscapeLikeLiteralEnMySqlReal_NoEvaluaComodinesPorcentajeNiGuionBajoYRespetaCeros()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        var conGuionBajo = await SembrarSolicitudAsync(
            provider, "00045_AbC/2026", "k-like-1", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 20));
        await SembrarSolicitudAsync(
            provider, "00045-AbC/2026", "k-like-2", new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 21));
        await SembrarSolicitudAsync(
            provider, "0045_AbC/2026", "k-like-3", new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 22));

        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISolicitudListadoReadRepository>();
        var hoy = new DateOnly(2026, 5, 15);

        FiltroSolicitudesListado CrearFiltro(string? folioParcial = null, DateOnly? fechaExacta = null, int page = 1) =>
            new(
                HoyMexico: hoy,
                FolioParcialNormalizado: folioParcial,
                FechaBusquedaExacta: fechaExacta,
                FechaEntregaDesde: null,
                FechaEntregaHasta: null,
                FechaVencimientoDesde: null,
                FechaVencimientoHasta: null,
                SoloNoEntregadas: false,
                VencimientoEstrictoMenorQue: null,
                DiasVencimientoDesde: null,
                DiasVencimientoHasta: null,
                OrdenCampo: OrdenListadoSolicitudesCampo.Folio,
                OrdenDescendente: false,
                Page: page,
                PageSize: 10);

        // '_' debe actuar como carácter literal y NO coincidir con '-'
        var resGuionBajo = await repo.ListarAsync(CrearFiltro(folioParcial: "00045_ABC"));
        Assert.Equal(LecturaSolicitudesListadoEstado.Consultado, resGuionBajo.Estado);
        Assert.Equal(1, resGuionBajo.TotalItems);
        Assert.Equal(conGuionBajo.Id, Assert.Single(resGuionBajo.Items).Id);

        // '%' y '!' literales no deben coincidir con ningún folio normal
        var resPorcentaje = await repo.ListarAsync(CrearFiltro(folioParcial: "%"));
        Assert.Equal(0, resPorcentaje.TotalItems);
        Assert.Empty(resPorcentaje.Items);

        var resExclamacion = await repo.ListarAsync(CrearFiltro(folioParcial: "!"));
        Assert.Equal(0, resExclamacion.TotalItems);

        // Fecha exacta coincide por FechaEntrega o FechaVencimiento
        var resFecha = await repo.ListarAsync(CrearFiltro(fechaExacta: new DateOnly(2026, 5, 21)));
        Assert.Equal(1, resFecha.TotalItems);
        Assert.Equal("00045-AbC/2026", resFecha.Items[0].Folio);

        // Página muy alta (int.MaxValue) evita overflow y retorna Items vacío con TotalItems exacto
        var resOverflowSafe = await repo.ListarAsync(CrearFiltro(page: int.MaxValue));
        Assert.Equal(3, resOverflowSafe.TotalItems);
        Assert.Empty(resOverflowSafe.Items);
    }

    [Fact]
    public async Task ConcurrenciaEliminacionYEdicion_RazaRealEnMySql_GarantizaPrecondicionFallidaONoEncontrada()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        // 1. Versión obsoleta tras edición previa -> PrecondicionFallida sin borrar ni insertar tombstone
        var sol1 = await SembrarSolicitudAsync(
            provider, "CONC-DEL-01", "idem-cdel-1", new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 25));
        var v1 = await ObtenerVersionActualAsync(provider, "CONC-DEL-01");

        using (var scopeEdit = provider.CreateScope())
        {
            var editRepo = scopeEdit.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            var edited = await editRepo.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sol1.Id, v1, AreaActiva2),
                s => s.ActualizarDatos(
                    new DateOnly(2026, 5, 5),
                    new TimeOnly(12, 0),
                    AreaActiva2,
                    "Editado antes de borrar",
                    MedioEntrega.Digital,
                    EstatusEntrega.EnProceso,
                    TipoSolicitud.Informacion,
                    TipoEvidencia.Carpeta,
                    false,
                    TipoAcuse.NoAplica,
                    new DateOnly(2026, 5, 25)));
            Assert.Equal(EdicionSolicitudEstado.Actualizada, edited.Estado);
        }

        using (var scopeDelStale = provider.CreateScope())
        {
            var delRepo = scopeDelStale.ServiceProvider.GetRequiredService<ISolicitudEliminacionPersistence>();
            var rStale = await delRepo.EjecutarEliminacionAsync(
                new EliminacionSolicitudPersistRequest(sol1.Id, v1, DateTime.UtcNow));
            Assert.Equal(EliminacionSolicitudEstado.PrecondicionFallida, rStale.Estado);
        }

        await using (var db = fixture.CreateDbContext())
        {
            Assert.True(await db.Solicitudes.AsNoTracking().AnyAsync(s => s.Id == sol1.Id));
            var tombstones = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM solicitud_idempotencias_eliminadas WHERE solicitud_id = {0}", sol1.Id.ToString())
                .SingleAsync();
            Assert.Equal(0, tombstones);
        }

        // 2. Dos eliminaciones paralelas con misma versión vigente -> una Eliminada y la otra NoEncontrada
        var sol2 = await SembrarSolicitudAsync(
            provider, "CONC-DEL-02", "idem-cdel-2", new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 25));
        var v2 = await ObtenerVersionActualAsync(provider, "CONC-DEL-02");

        using var barrier = new Barrier(2);
        var tasks = Enumerable.Range(1, 2).Select(_ => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var delRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEliminacionPersistence>();
            barrier.SignalAndWait();
            return await delRepo.EjecutarEliminacionAsync(
                new EliminacionSolicitudPersistRequest(sol2.Id, v2, DateTime.UtcNow));
        })).ToArray();

        var resultados = await Task.WhenAll(tasks);
        Assert.Single(resultados, r => r.Estado == EliminacionSolicitudEstado.Eliminada);
        Assert.Single(resultados, r => r.Estado == EliminacionSolicitudEstado.NoEncontrada);
    }

    [Fact]
    public async Task ConflictoIntegridad1451_ConTablaHijaAuxiliarRestrict_HaceRollbackSinBorrarNiDejarTombstoneHuerfano()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        var sol = await SembrarSolicitudAsync(
            provider, "FK-DEL-1451", "idem-fk-1451", new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 20));
        var version = await ObtenerVersionActualAsync(provider, "FK-DEL-1451");

        const string childTable = "aux_solicitud_referencia_test_1451";
        await using (var dbSetup = fixture.CreateDbContext())
        {
            await dbSetup.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE `{childTable}` (
                    `id` int NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    `solicitud_id` char(36) NOT NULL,
                    CONSTRAINT `FK_{childTable}_solicitudes`
                        FOREIGN KEY (`solicitud_id`) REFERENCES `solicitudes` (`id`) ON DELETE RESTRICT
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await dbSetup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO `aux_solicitud_referencia_test_1451` (`solicitud_id`) VALUES ({sol.Id.ToString()});");
        }

        try
        {
            using var scope = provider.CreateScope();
            var delRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEliminacionPersistence>();
            var outcome = await delRepo.EjecutarEliminacionAsync(
                new EliminacionSolicitudPersistRequest(sol.Id, version, DateTime.UtcNow));

            Assert.Equal(EliminacionSolicitudEstado.ConflictoIntegridad, outcome.Estado);

            await using var dbVerify = fixture.CreateDbContext();
            Assert.True(await dbVerify.Solicitudes.AsNoTracking().AnyAsync(s => s.Id == sol.Id));

            var tombstones = await dbVerify.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM solicitud_idempotencias_eliminadas WHERE idempotency_key = {0}",
                    "idem-fk-1451")
                .SingleAsync();
            Assert.Equal(0, tombstones);
        }
        finally
        {
            await using var dbCleanup = fixture.CreateDbContext();
            await dbCleanup.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS `{childTable}`;");
        }
    }

    [Fact]
    public async Task TombstoneIdempotencia_BloqueaReusoEnMismoUsuarioSinAvanzarSecuencia_YPermiteOtroUsuario()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        const string claveCompartida = "idem-tombstone-shared";
        var creada = await SembrarSolicitudAsync(
            provider,
            folio: string.Empty,
            idempotencyKey: claveCompartida,
            fechaEntrega: new DateOnly(2026, 5, 10),
            fechaVencimiento: new DateOnly(2026, 5, 20),
            automatico: true,
            usuarioId: fixture.AdminUserId);

        Assert.Equal("SOL-2026-001", creada.Folio);
        var version = await ObtenerVersionActualAsync(provider, creada.FolioNormalizado);

        using (var scopeDel = provider.CreateScope())
        {
            var delRepo = scopeDel.ServiceProvider.GetRequiredService<ISolicitudEliminacionPersistence>();
            var delRes = await delRepo.EjecutarEliminacionAsync(
                new EliminacionSolicitudPersistRequest(creada.Id, version, DateTime.UtcNow));
            Assert.Equal(EliminacionSolicitudEstado.Eliminada, delRes.Estado);
        }

        // Reintento automático y manual con el mismo usuario Admin -> IdempotenciaSolicitudEliminada y secuencia sigue en 1
        foreach (var esAutomatico in new[] { true, false })
        {
            using var scopeRetry = provider.CreateScope();
            var regRepo = scopeRetry.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
            var retry = await regRepo.EjecutarCreacionAsync(
                new RegistroSolicitudPersistRequest(
                    fixture.AdminUserId,
                    claveCompartida,
                    new string('9', 64),
                    esAutomatico,
                    esAutomatico ? null : "MANUAL-REINTENTO-01",
                    AreaActiva1,
                    2026),
                f => Solicitud.Crear(
                    f,
                    new DateOnly(2026, 5, 10),
                    new TimeOnly(11, 0),
                    AreaActiva1,
                    "No debe recrearse",
                    MedioEntrega.Digital,
                    EstatusEntrega.EnProceso,
                    TipoSolicitud.Informacion,
                    TipoEvidencia.Carpeta,
                    false,
                    TipoAcuse.NoAplica,
                    new DateOnly(2026, 5, 20),
                    fixture.AdminUserId,
                    DateTime.UtcNow,
                    esAutomatico));

            Assert.Equal(RegistroSolicitudEstado.IdempotenciaSolicitudEliminada, retry.Estado);
        }

        await using (var dbCheck = fixture.CreateDbContext())
        {
            Assert.Equal(0, await dbCheck.Solicitudes.CountAsync());
            var secuencia = await dbCheck.Database
                .SqlQueryRaw<int>("SELECT ultimo_numero AS Value FROM folio_secuencias WHERE anio = 2026")
                .SingleAsync();
            Assert.Equal(1, secuencia);
        }

        // Otro usuario distinto (ConsultaUserId) sí puede registrar con la misma clave porque el tombstone es por usuario
        var otroUsuario = await SembrarSolicitudAsync(
            provider,
            folio: string.Empty,
            idempotencyKey: claveCompartida,
            fechaEntrega: new DateOnly(2026, 5, 10),
            fechaVencimiento: new DateOnly(2026, 5, 22),
            automatico: true,
            usuarioId: fixture.ConsultaUserId);

        Assert.Equal("SOL-2026-002", otroUsuario.Folio);
    }

    [Fact]
    public async Task FallaDeDisponibilidad_EnListadoYEliminacion_RetornaPersistenciaNoDisponibleSinExcepcion()
    {
        var invalidPwd = Guid.NewGuid().ToString("N");
        var closedPortConn = $"Server=127.0.0.1;Port=39997;Database=no_existe;Uid=u;Pwd={invalidPwd};Connection Timeout=2;";
        await using var provider = BuildProvider(closedPortConn);

        using var scope = provider.CreateScope();
        var listadoRepo = scope.ServiceProvider.GetRequiredService<ISolicitudListadoReadRepository>();
        var listado = await listadoRepo.ListarAsync(new FiltroSolicitudesListado(
            new DateOnly(2026, 5, 15),
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            OrdenListadoSolicitudesCampo.Vencimiento,
            false,
            1,
            10));
        Assert.Equal(LecturaSolicitudesListadoEstado.PersistenciaNoDisponible, listado.Estado);

        var eliminacionRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEliminacionPersistence>();
        var eliminacion = await eliminacionRepo.EjecutarEliminacionAsync(
            new EliminacionSolicitudPersistRequest(Guid.CreateVersion7(), new string('a', 32), DateTime.UtcNow));
        Assert.Equal(EliminacionSolicitudEstado.PersistenciaNoDisponible, eliminacion.Estado);
    }
}
