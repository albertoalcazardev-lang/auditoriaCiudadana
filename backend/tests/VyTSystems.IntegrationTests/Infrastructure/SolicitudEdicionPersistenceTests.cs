using System.Text.RegularExpressions;
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
public sealed partial class SolicitudEdicionPersistenceTests(MySqlTestFixture fixture)
{
    private static readonly Guid AreaActiva1 = Guid.Parse("01950000-0000-7000-8000-000000000001");
    private static readonly Guid AreaActiva2 = Guid.Parse("01950000-0000-7000-8000-000000000002");

    [GeneratedRegex(@"^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerHex32Regex();

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
        string payloadHash)
    {
        using var scope = provider.CreateScope();
        var registro = scope.ServiceProvider.GetRequiredService<ISolicitudRegistroPersistence>();
        var outcome = await registro.EjecutarCreacionAsync(
            new RegistroSolicitudPersistRequest(
                fixture.AdminUserId,
                idempotencyKey,
                payloadHash,
                false,
                folio,
                AreaActiva1,
                2026),
            f => Solicitud.Crear(
                f,
                new DateOnly(2026, 5, 10),
                new TimeOnly(11, 20),
                AreaActiva1,
                "Asunto inicial sembrado",
                MedioEntrega.Digital,
                EstatusEntrega.EnProceso,
                TipoSolicitud.Informacion,
                TipoEvidencia.Carpeta,
                true,
                TipoAcuse.Sello,
                new DateOnly(2026, 5, 25),
                fixture.AdminUserId,
                new DateTime(2026, 5, 10, 17, 20, 0, DateTimeKind.Utc),
                false));

        Assert.Equal(RegistroSolicitudEstado.Creada, outcome.Estado);
        return outcome.Solicitud!;
    }

    [Fact]
    public async Task ObtenerPorFolioNormalizado_ConBarrasGuionesYCerosIniciales_DistingueCerosYRetornaVersion()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        var conTresCeros = await SembrarSolicitudAsync(provider, "00045-AbC/2026_1.0", "idem-ed-1", new string('1', 64));
        var conDosCeros = await SembrarSolicitudAsync(provider, "0045-AbC/2026_1.0", "idem-ed-2", new string('2', 64));

        using var scope = provider.CreateScope();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();

        var r1 = await readRepo.ObtenerPorFolioNormalizadoAsync(Solicitud.NormalizarFolio("00045-abc/2026_1.0"));
        Assert.Equal(LecturaSolicitudEdicionEstado.Encontrada, r1.Estado);
        Assert.Equal(conTresCeros.Id, r1.Solicitud!.Id);
        Assert.Equal("00045-AbC/2026_1.0", r1.Solicitud.Folio);
        Assert.Matches(LowerHex32Regex(), r1.VersionConcurrencia!);

        var r2 = await readRepo.ObtenerPorFolioNormalizadoAsync(Solicitud.NormalizarFolio("0045-ABC/2026_1.0"));
        Assert.Equal(LecturaSolicitudEdicionEstado.Encontrada, r2.Estado);
        Assert.Equal(conDosCeros.Id, r2.Solicitud!.Id);
        Assert.Equal("0045-AbC/2026_1.0", r2.Solicitud.Folio);

        var rNoExiste = await readRepo.ObtenerPorFolioNormalizadoAsync("NO-EXISTE-2026");
        Assert.Equal(LecturaSolicitudEdicionEstado.NoEncontrada, rNoExiste.Estado);
    }

    [Fact]
    public async Task EjecutarEdicion_ActualizaAtomicamenteRotaVersionYPreservaFolioIdAuditoriaEIdempotencia()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        const string idemKey = "idem-preserve-bit-a-bit";
        var idemHash = new string('f', 64);
        var sembrada = await SembrarSolicitudAsync(provider, "0099-XyZ/2026", idemKey, idemHash);

        string v1;
        using (var scopeRead = provider.CreateScope())
        {
            var readRepo = scopeRead.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
            var precarga = await readRepo.ObtenerPorFolioNormalizadoAsync("0099-XYZ/2026");
            v1 = precarga.VersionConcurrencia!;
        }

        string v2;
        using (var scopeEdit = provider.CreateScope())
        {
            var editRepo = scopeEdit.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            var outcome = await editRepo.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sembrada.Id, v1, AreaActiva2),
                s => s.ActualizarDatos(
                    new DateOnly(2026, 6, 1),
                    new TimeOnly(15, 45),
                    AreaActiva2,
                    "Asunto actualizado atómicamente",
                    MedioEntrega.CorreoElectronico,
                    EstatusEntrega.Entregada,
                    TipoSolicitud.Aclaracion,
                    TipoEvidencia.Link,
                    false,
                    TipoAcuse.NoAplica,
                    new DateOnly(2026, 6, 12)));

            Assert.Equal(EdicionSolicitudEstado.Actualizada, outcome.Estado);
            v2 = outcome.VersionConcurrenciaActual!;
            Assert.Matches(LowerHex32Regex(), v2);
            Assert.NotEqual(v1, v2);
        }

        // Verificar persistencia real bit a bit en MySQL incluyendo columnas sombra de idempotencia
        await using var db = fixture.CreateDbContext();
        var verificada = await db.Solicitudes
            .AsNoTracking()
            .Where(s => s.Id == sembrada.Id)
            .Select(s => new
            {
                Entidad = s,
                IdempotencyKey = EF.Property<string>(s, "IdempotencyKey"),
                IdempotencyPayloadHash = EF.Property<string>(s, "IdempotencyPayloadHash"),
                ConcurrencyToken = EF.Property<string>(s, "ConcurrencyToken")
            })
            .SingleAsync();

        Assert.Equal(sembrada.Id, verificada.Entidad.Id);
        Assert.Equal("0099-XyZ/2026", verificada.Entidad.Folio);
        Assert.Equal("0099-XYZ/2026", verificada.Entidad.FolioNormalizado);
        Assert.False(verificada.Entidad.EsFolioAutomatico);
        Assert.Equal(sembrada.CreadoPorUsuarioId, verificada.Entidad.CreadoPorUsuarioId);
        Assert.Equal(sembrada.CreadoEnUtc, verificada.Entidad.CreadoEnUtc);
        Assert.Equal(idemKey, verificada.IdempotencyKey);
        Assert.Equal(idemHash, verificada.IdempotencyPayloadHash);
        Assert.Equal(v2, verificada.ConcurrencyToken);
        Assert.Equal(AreaActiva2, verificada.Entidad.AreaReceptoraId);
        Assert.Equal("Asunto actualizado atómicamente", verificada.Entidad.Asunto);
        Assert.Equal(EstatusEntrega.Entregada, verificada.Entidad.EstatusEntrega);

        // Guardar exactamente los mismos 11 valores también rota la versión de concurrencia
        using (var scopeNoOp = provider.CreateScope())
        {
            var editRepo = scopeNoOp.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            var outcomeNoOp = await editRepo.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sembrada.Id, v2, AreaActiva2),
                s => s.ActualizarDatos(
                    new DateOnly(2026, 6, 1),
                    new TimeOnly(15, 45),
                    AreaActiva2,
                    "Asunto actualizado atómicamente",
                    MedioEntrega.CorreoElectronico,
                    EstatusEntrega.Entregada,
                    TipoSolicitud.Aclaracion,
                    TipoEvidencia.Link,
                    false,
                    TipoAcuse.NoAplica,
                    new DateOnly(2026, 6, 12)));

            Assert.Equal(EdicionSolicitudEstado.Actualizada, outcomeNoOp.Estado);
            Assert.NotEqual(v2, outcomeNoOp.VersionConcurrenciaActual);
        }
    }

    [Fact]
    public async Task ConcurrenciaReal_DosDbContextsConMismaVersionV1_UnoGanaYSegundoRecibeConflictoSinSobrescribir()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        var sembrada = await SembrarSolicitudAsync(provider, "CONC-EDIT-001", "idem-conc-1", new string('a', 64));

        string v1;
        using (var scopeRead = provider.CreateScope())
        {
            var readRepo = scopeRead.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
            v1 = (await readRepo.ObtenerPorFolioNormalizadoAsync("CONC-EDIT-001")).VersionConcurrencia!;
        }

        // Sincronizamos para que ambos contextos carguen la entidad rastreada con v1 antes de que cualquiera haga SaveChanges
        using var ambosCargados = new Barrier(2);
        var t1 = Task.Run(async () =>
        {
            using var scope1 = provider.CreateScope();
            var repo1 = scope1.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            return await repo1.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sembrada.Id, v1, AreaActiva1),
                s =>
                {
                    ambosCargados.SignalAndWait();
                    s.ActualizarDatos(
                        new DateOnly(2026, 5, 11),
                        new TimeOnly(9, 0),
                        AreaActiva1,
                        "Actualización Contexto 1",
                        MedioEntrega.Digital,
                        EstatusEntrega.EnProceso,
                        TipoSolicitud.Informacion,
                        TipoEvidencia.Carpeta,
                        false,
                        TipoAcuse.NoAplica,
                        new DateOnly(2026, 5, 25));
                });
        });

        var t2 = Task.Run(async () =>
        {
            using var scope2 = provider.CreateScope();
            var repo2 = scope2.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            return await repo2.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sembrada.Id, v1, AreaActiva2),
                s =>
                {
                    ambosCargados.SignalAndWait();
                    s.ActualizarDatos(
                        new DateOnly(2026, 5, 12),
                        new TimeOnly(14, 0),
                        AreaActiva2,
                        "Actualización Contexto 2",
                        MedioEntrega.PortalWeb,
                        EstatusEntrega.Entregada,
                        TipoSolicitud.Otro,
                        TipoEvidencia.Link,
                        true,
                        TipoAcuse.Electronico,
                        new DateOnly(2026, 5, 28));
                });
        });

        var resultados = await Task.WhenAll(t1, t2);

        var ganador = Assert.Single(resultados, r => r.Estado == EdicionSolicitudEstado.Actualizada);
        Assert.Single(resultados, r => r.Estado == EdicionSolicitudEstado.ConflictoConcurrencia);

        await using var db = fixture.CreateDbContext();
        var vigente = await db.Solicitudes.AsNoTracking().SingleAsync(s => s.Id == sembrada.Id);
        Assert.Equal(ganador.Solicitud!.Asunto, vigente.Asunto);
        Assert.Equal(ganador.Solicitud.AreaReceptoraId, vigente.AreaReceptoraId);
    }

    [Fact]
    public async Task EliminacionConcurrente_AntesODuranteEdicion_RetornaNoEncontrada()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        // 1. Id inexistente desde el inicio
        using (var scopeMissing = provider.CreateScope())
        {
            var repo = scopeMissing.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            var rMissing = await repo.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(Guid.CreateVersion7(), new string('0', 32), AreaActiva1),
                _ => { });
            Assert.Equal(EdicionSolicitudEstado.NoEncontrada, rMissing.Estado);
        }

        // 2. Eliminación concurrente entre la lectura rastreada y SaveChangesAsync (DbUpdateConcurrencyException -> NoEncontrada)
        var sembrada = await SembrarSolicitudAsync(provider, "DEL-CONC-001", "idem-del-1", new string('b', 64));
        string v1;
        using (var scopeRead = provider.CreateScope())
        {
            var readRepo = scopeRead.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
            v1 = (await readRepo.ObtenerPorFolioNormalizadoAsync("DEL-CONC-001")).VersionConcurrencia!;
        }

        using (var scopeEdit = provider.CreateScope())
        {
            var repo = scopeEdit.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
            var outcome = await repo.EjecutarEdicionAsync(
                new EdicionSolicitudPersistRequest(sembrada.Id, v1, AreaActiva1),
                s =>
                {
                    // Otra conexión elimina el registro antes de que este DbContext ejecute UPDATE
                    using var dbParalelo = fixture.CreateDbContext();
                    dbParalelo.Database.ExecuteSqlInterpolated($"DELETE FROM solicitudes WHERE id = {sembrada.Id};");

                    s.ActualizarDatos(
                        new DateOnly(2026, 5, 11),
                        new TimeOnly(10, 0),
                        AreaActiva1,
                        "Asunto sobre fila eliminada",
                        MedioEntrega.Digital,
                        EstatusEntrega.EnProceso,
                        TipoSolicitud.Informacion,
                        TipoEvidencia.Carpeta,
                        false,
                        TipoAcuse.NoAplica,
                        new DateOnly(2026, 5, 20));
                });

            Assert.Equal(EdicionSolicitudEstado.NoEncontrada, outcome.Estado);
        }
    }

    [Fact]
    public async Task AreaReceptoraInexistenteOInactiva_RetornaAreaReceptoraInvalidaSinModificarDatos()
    {
        await fixture.ResetSolicitudesAsync();
        await using var provider = BuildProvider();

        var sembrada = await SembrarSolicitudAsync(provider, "AREA-INV-001", "idem-area-inv", new string('c', 64));
        string v1;
        using (var scopeRead = provider.CreateScope())
        {
            var readRepo = scopeRead.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
            v1 = (await readRepo.ObtenerPorFolioNormalizadoAsync("AREA-INV-001")).VersionConcurrencia!;
        }

        var areaInactivaId = Guid.Parse("01950000-0000-7000-8000-000000000097");
        await using (var dbSeed = fixture.CreateDbContext())
        {
            await dbSeed.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO areas_receptoras (id, nombre, activa, orden)
                VALUES ({areaInactivaId}, 'Área Inactiva Edición', 0, 97)
                ON DUPLICATE KEY UPDATE activa = 0;");
        }

        try
        {
            foreach (var areaInvalida in new[] { Guid.Parse("01950000-0000-7000-8000-000000000999"), areaInactivaId })
            {
                using var scopeEdit = provider.CreateScope();
                var editRepo = scopeEdit.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
                var outcome = await editRepo.EjecutarEdicionAsync(
                    new EdicionSolicitudPersistRequest(sembrada.Id, v1, areaInvalida),
                    s => s.ActualizarDatos(
                        new DateOnly(2026, 6, 1),
                        new TimeOnly(10, 0),
                        areaInvalida,
                        "No debe persistirse",
                        MedioEntrega.Digital,
                        EstatusEntrega.Entregada,
                        TipoSolicitud.Informacion,
                        TipoEvidencia.Carpeta,
                        false,
                        TipoAcuse.NoAplica,
                        new DateOnly(2026, 6, 10)));

                Assert.Equal(EdicionSolicitudEstado.AreaReceptoraInvalida, outcome.Estado);
            }

            await using var dbCheck = fixture.CreateDbContext();
            var actual = await dbCheck.Solicitudes.AsNoTracking().SingleAsync(s => s.Id == sembrada.Id);
            Assert.Equal("Asunto inicial sembrado", actual.Asunto);
            Assert.Equal(AreaActiva1, actual.AreaReceptoraId);
        }
        finally
        {
            await using var dbCleanup = fixture.CreateDbContext();
            await dbCleanup.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM areas_receptoras WHERE id = {areaInactivaId};");
        }
    }

    [Fact]
    public async Task FallaDeDisponibilidad_RetornaPersistenciaNoDisponibleEnLecturaYEscritura()
    {
        var invalidPwd = Guid.NewGuid().ToString("N");
        var closedPortConn = $"Server=127.0.0.1;Port=39998;Database=no_existe;Uid=u;Pwd={invalidPwd};Connection Timeout=2;";
        await using var provider = BuildProvider(closedPortConn);

        using var scope = provider.CreateScope();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEdicionReadRepository>();
        var lectura = await readRepo.ObtenerPorFolioNormalizadoAsync("SOL-OFFLINE");
        Assert.Equal(LecturaSolicitudEdicionEstado.PersistenciaNoDisponible, lectura.Estado);

        var editRepo = scope.ServiceProvider.GetRequiredService<ISolicitudEdicionPersistence>();
        var edicion = await editRepo.EjecutarEdicionAsync(
            new EdicionSolicitudPersistRequest(Guid.CreateVersion7(), new string('0', 32), AreaActiva1),
            _ => { });
        Assert.Equal(EdicionSolicitudEstado.PersistenciaNoDisponible, edicion.Estado);
    }
}
