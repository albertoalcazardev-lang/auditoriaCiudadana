using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using Testcontainers.MySql;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Infrastructure;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class MigrationAndSchemaTests(MySqlTestFixture fixture)
{
    [Fact]
    public async Task MigrateDownYUp_EnBaseAislada_AplicaYRevierteEsquemaCon10AreasSemilla()
    {
        var password = $"Mig_{Guid.NewGuid():N}!9a";
        await using var container = new MySqlBuilder("mysql:8.4")
            .WithDatabase($"vyt_mig_{Guid.NewGuid():N}")
            .WithUsername("vyt_mig")
            .WithPassword(password)
            .Build();

        await container.StartAsync();

        await using var db = fixture.CreateDbContext(container.GetConnectionString());
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var areas = await db.AreasReceptoras.AsNoTracking().OrderBy(a => a.Orden).ToListAsync();
        Assert.Equal(10, areas.Count);
        Assert.Equal("Dirección de Obras Públicas", areas[0].Nombre);
        Assert.Equal(Guid.Parse("01950000-0000-7000-8000-000000000001"), areas[0].Id);
        Assert.Equal("Dirección de Servicios Públicos", areas[9].Nombre);
        Assert.Equal(Guid.Parse("01950000-0000-7000-8000-000000000010"), areas[9].Id);

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261126002109_InitialIdentity");

        var tablasTrasDown = await ObtenerTablasAsync(container.GetConnectionString());
        Assert.DoesNotContain("solicitudes", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("folio_secuencias", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("areas_receptoras", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AspNetUsers", tablasTrasDown, StringComparer.OrdinalIgnoreCase);

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var tablasTrasUp = await ObtenerTablasAsync(container.GetConnectionString());
        Assert.Contains("solicitudes", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("folio_secuencias", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("areas_receptoras", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(10, await db.AreasReceptoras.CountAsync());
    }

    [Fact]
    public async Task MySqlConstraints_CheckUniqueYForeignKeyRestrict_AplicanEnBaseReal()
    {
        await fixture.ResetSolicitudesAsync();
        await using var db = fixture.CreateDbContext();

        var areaId = Guid.Parse("01950000-0000-7000-8000-000000000001");
        var id1 = Guid.CreateVersion7();
        var id2 = Guid.CreateVersion7();

        // CHECK constraint: fecha_vencimiento < fecha_entrega debe fallar con error 3819
        var exCheck = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO solicitudes
                    (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                     area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                     tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                     creado_en_utc, idempotency_key, idempotency_payload_hash)
                VALUES
                    ({id1}, 'CHK-001', 'CHK-001', 0, '2026-05-10', '10:00:00',
                     {areaId}, 'Asunto válido', 'Digital', 'EnProceso', 'Informacion',
                     'Carpeta', 0, 'NoAplica', '2026-05-09', {fixture.AdminUserId},
                     '2026-05-10 10:00:00', 'k-chk-1', {new string('a', 64)});"));
        Assert.Equal(3819, exCheck.Number);

        // CHECK constraint: estatus_entrega = 'Vencida' debe fallar con error 3819
        var exEstatus = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO solicitudes
                    (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                     area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                     tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                     creado_en_utc, idempotency_key, idempotency_payload_hash)
                VALUES
                    ({id2}, 'CHK-002', 'CHK-002', 0, '2026-05-10', '10:00:00',
                     {areaId}, 'Asunto válido', 'Digital', 'Vencida', 'Informacion',
                     'Carpeta', 0, 'NoAplica', '2026-05-15', {fixture.AdminUserId},
                     '2026-05-10 10:00:00', 'k-chk-2', {new string('b', 64)});"));
        Assert.Equal(3819, exEstatus.Number);

        // Inserción válida + intento de borrar área receptora referenciada debe fallar por ON DELETE RESTRICT (1451)
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO solicitudes
                (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                 area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                 tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                 creado_en_utc, idempotency_key, idempotency_payload_hash)
            VALUES
                ({id1}, 'FK-001', 'FK-001', 0, '2026-05-10', '10:00:00',
                 {areaId}, 'Asunto válido', 'Digital', 'EnProceso', 'Informacion',
                 'Carpeta', 0, 'NoAplica', '2026-05-15', {fixture.AdminUserId},
                 '2026-05-10 10:00:00', 'k-fk-1', {new string('c', 64)});");

        var exFkRestrict = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM areas_receptoras WHERE id = {areaId};"));
        Assert.Equal(1451, exFkRestrict.Number);
    }

    private static async Task<HashSet<string>> ObtenerTablasAsync(string connectionString)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SHOW TABLES;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
