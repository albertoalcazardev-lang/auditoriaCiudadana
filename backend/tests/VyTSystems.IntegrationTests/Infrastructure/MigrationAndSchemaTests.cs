using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using Testcontainers.MySql;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Infrastructure;

[Collection(MySqlIntegrationCollection.Name)]
public sealed partial class MigrationAndSchemaTests(MySqlTestFixture fixture)
{
    [GeneratedRegex(@"^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerHex32Regex();

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
        Assert.DoesNotContain("solicitud_idempotencias_eliminadas", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("folio_secuencias", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("areas_receptoras", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AspNetUsers", tablasTrasDown, StringComparer.OrdinalIgnoreCase);

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var tablasTrasUp = await ObtenerTablasAsync(container.GetConnectionString());
        Assert.Contains("solicitudes", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("solicitud_idempotencias_eliminadas", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("folio_secuencias", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("areas_receptoras", tablasTrasUp, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(10, await db.AreasReceptoras.CountAsync());
    }

    [Fact]
    public async Task MigracionIncrementalConcurrencyToken_ConFilasHistoricasPrevias_BackfilleaYPermiteDownYUp()
    {
        var password = $"MigHist_{Guid.NewGuid():N}!9a";
        await using var container = new MySqlBuilder("mysql:8.4")
            .WithDatabase($"vyt_mighist_{Guid.NewGuid():N}")
            .WithUsername("vyt_mig")
            .WithPassword(password)
            .Build();

        await container.StartAsync();
        var connStr = container.GetConnectionString();

        await using var db = fixture.CreateDbContext(connStr);
        var migrator = db.GetService<IMigrator>();

        // 1. Migrar únicamente hasta Task 02 (antes de que exista concurrency_token)
        await migrator.MigrateAsync("20261126060345_AddSolicitudesYAreasReceptoras");

        var columnasPre = await ObtenerColumnasTablaAsync(connStr, "solicitudes");
        Assert.DoesNotContain("concurrency_token", columnasPre, StringComparer.OrdinalIgnoreCase);

        var userId = Guid.CreateVersion7().ToString();
        var areaId = Guid.Parse("01950000-0000-7000-8000-000000000001");
        var sol1Id = Guid.CreateVersion7();
        var sol2Id = Guid.CreateVersion7();

        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO AspNetUsers
                (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                 PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES
                ({userId}, 'hist_admin', 'HIST_ADMIN', 'hist@vyt.test', 'HIST@VYT.TEST', 1, 0, 0, 1, 0);");

        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO solicitudes
                (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                 area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                 tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                 creado_en_utc, idempotency_key, idempotency_payload_hash)
            VALUES
                ({sol1Id}, 'HIST-001', 'HIST-001', 0, '2026-05-01', '09:00:00',
                 {areaId}, 'Histórica uno', 'Digital', 'EnProceso', 'Informacion',
                 'Carpeta', 0, 'NoAplica', '2026-05-15', {userId},
                 '2026-05-01 15:00:00', 'idem-hist-1', {new string('1', 64)}),
                ({sol2Id}, 'HIST-002', 'HIST-002', 0, '2026-05-02', '10:30:00',
                 {areaId}, 'Histórica dos', 'Impreso', 'Entregada', 'Aclaracion',
                 'Link', 1, 'Sello', '2026-05-20', {userId},
                 '2026-05-02 16:30:00', 'idem-hist-2', {new string('2', 64)});");

        // 2. Aplicar AddSolicitudConcurrencyToken sobre tabla poblada
        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var tokens = await db.Database
            .SqlQueryRaw<string>("SELECT concurrency_token AS Value FROM solicitudes ORDER BY folio")
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.Matches(LowerHex32Regex(), t));
        Assert.NotEqual(tokens[0], tokens[1]);

        // 3. Revertir AddSolicitudConcurrencyToken (Down) y verificar preservación de filas históricas
        await migrator.MigrateAsync("20261126060345_AddSolicitudesYAreasReceptoras");
        var columnasTrasDown = await ObtenerColumnasTablaAsync(connStr, "solicitudes");
        Assert.DoesNotContain("concurrency_token", columnasTrasDown, StringComparer.OrdinalIgnoreCase);

        var conteoTrasDown = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM solicitudes")
            .SingleAsync();
        Assert.Equal(2, conteoTrasDown);

        // 4. Volver a aplicar Up
        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(2, await db.Solicitudes.CountAsync());
    }

    [Fact]
    public async Task MySqlConstraints_CheckUniqueYForeignKeyRestrict_AplicanEnBaseReal()
    {
        await fixture.ResetSolicitudesAsync();
        await using var db = fixture.CreateDbContext();

        var areaId = Guid.Parse("01950000-0000-7000-8000-000000000001");
        var id1 = Guid.CreateVersion7();
        var id2 = Guid.CreateVersion7();
        var id3 = Guid.CreateVersion7();
        const string tokenValido = "0123456789abcdef0123456789abcdef";

        // CHECK constraint: fecha_vencimiento < fecha_entrega debe fallar con error 3819
        var exCheck = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO solicitudes
                    (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                     area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                     tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                     creado_en_utc, idempotency_key, idempotency_payload_hash, concurrency_token)
                VALUES
                    ({id1}, 'CHK-001', 'CHK-001', 0, '2026-05-10', '10:00:00',
                     {areaId}, 'Asunto válido', 'Digital', 'EnProceso', 'Informacion',
                     'Carpeta', 0, 'NoAplica', '2026-05-09', {fixture.AdminUserId},
                     '2026-05-10 10:00:00', 'k-chk-1', {new string('a', 64)}, {tokenValido});"));
        Assert.Equal(3819, exCheck.Number);

        // CHECK constraint: estatus_entrega = 'Vencida' debe fallar con error 3819
        var exEstatus = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO solicitudes
                    (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                     area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                     tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                     creado_en_utc, idempotency_key, idempotency_payload_hash, concurrency_token)
                VALUES
                    ({id2}, 'CHK-002', 'CHK-002', 0, '2026-05-10', '10:00:00',
                     {areaId}, 'Asunto válido', 'Digital', 'Vencida', 'Informacion',
                     'Carpeta', 0, 'NoAplica', '2026-05-15', {fixture.AdminUserId},
                     '2026-05-10 10:00:00', 'k-chk-2', {new string('b', 64)}, {tokenValido});"));
        Assert.Equal(3819, exEstatus.Number);

        // CHECK constraint: concurrency_token inválido (corto, mayúsculas o no-hex) debe fallar con error 3819
        foreach (var tokenInvalido in new[] { "corto", "0123456789ABCDEF0123456789abcdef", new string('g', 32) })
        {
            var exToken = await Assert.ThrowsAsync<MySqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO solicitudes
                        (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                         area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                         tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                         creado_en_utc, idempotency_key, idempotency_payload_hash, concurrency_token)
                    VALUES
                        ({id3}, 'CHK-TOK', 'CHK-TOK', 0, '2026-05-10', '10:00:00',
                         {areaId}, 'Asunto válido', 'Digital', 'EnProceso', 'Informacion',
                         'Carpeta', 0, 'NoAplica', '2026-05-15', {fixture.AdminUserId},
                         '2026-05-10 10:00:00', 'k-chk-tok', {new string('d', 64)}, {tokenInvalido});"));
            Assert.Equal(3819, exToken.Number);
        }

        // Inserción válida + intento de borrar área receptora referenciada debe fallar por ON DELETE RESTRICT (1451)
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO solicitudes
                (id, folio, folio_normalizado, es_folio_automatico, fecha_entrega, hora_entrega,
                 area_receptora_id, asunto, medio_entrega, estatus_entrega, tipo_solicitud,
                 tipo_evidencia, tiene_acuse, tipo_acuse, fecha_vencimiento, creado_por_usuario_id,
                 creado_en_utc, idempotency_key, idempotency_payload_hash, concurrency_token)
            VALUES
                ({id1}, 'FK-001', 'FK-001', 0, '2026-05-10', '10:00:00',
                 {areaId}, 'Asunto válido', 'Digital', 'EnProceso', 'Informacion',
                 'Carpeta', 0, 'NoAplica', '2026-05-15', {fixture.AdminUserId},
                 '2026-05-10 10:00:00', 'k-fk-1', {new string('c', 64)}, {tokenValido});");

        var exFkRestrict = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM areas_receptoras WHERE id = {areaId};"));
        Assert.Equal(1451, exFkRestrict.Number);
    }

    [Fact]
    public async Task MigracionTombstoneEIndiceFechaEntrega_RevierteDownYUp_YValidaCollationCaseSensitiveYConstraints()
    {
        var password = $"MigTomb_{Guid.NewGuid():N}!9a";
        await using var container = new MySqlBuilder("mysql:8.4")
            .WithDatabase($"vyt_migtomb_{Guid.NewGuid():N}")
            .WithUsername("vyt_mig")
            .WithPassword(password)
            .Build();

        await container.StartAsync();
        var connStr = container.GetConnectionString();

        await using var db = fixture.CreateDbContext(connStr);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var indicesSolicitudes = await ObtenerIndicesTablaAsync(connStr, "solicitudes");
        Assert.Contains("IX_solicitudes_fecha_entrega", indicesSolicitudes, StringComparer.OrdinalIgnoreCase);

        var collationKey = await ObtenerCollationColumnaAsync(connStr, "solicitud_idempotencias_eliminadas", "idempotency_key");
        Assert.Equal("utf8mb4_0900_as_cs", collationKey);

        var userId = Guid.CreateVersion7().ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO AspNetUsers
                (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                 PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES
                ({userId}, 'tomb_admin', 'TOMB_ADMIN', 'tomb@vyt.test', 'TOMB@VYT.TEST', 1, 0, 0, 1, 0);");

        // PK compuesta con collation case-sensitive permite coexistencia de 'Key-Case-1' y 'key-case-1'
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO solicitud_idempotencias_eliminadas
                (creado_por_usuario_id, idempotency_key, idempotency_payload_hash, solicitud_id, eliminado_en_utc)
            VALUES
                ({userId}, 'Key-Case-1', {new string('a', 64)}, {Guid.CreateVersion7().ToString()}, '2026-05-15 12:00:00'),
                ({userId}, 'key-case-1', {new string('b', 64)}, {Guid.CreateVersion7().ToString()}, '2026-05-15 12:05:00');");

        // CHECK constraint en hash y FK ON DELETE RESTRICT hacia AspNetUsers
        var exHash = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO solicitud_idempotencias_eliminadas
                    (creado_por_usuario_id, idempotency_key, idempotency_payload_hash, solicitud_id, eliminado_en_utc)
                VALUES
                    ({userId}, 'bad-hash', 'corto', {Guid.CreateVersion7().ToString()}, '2026-05-15 12:10:00');"));
        Assert.Equal(3819, exHash.Number);

        var exDeleteUser = await Assert.ThrowsAsync<MySqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM AspNetUsers WHERE Id = {userId};"));
        Assert.Equal(1451, exDeleteUser.Number);

        // Down hasta AddSolicitudConcurrencyToken y vuelta a Up
        await migrator.MigrateAsync("20261126103404_AddSolicitudConcurrencyToken");
        var tablasTrasDown = await ObtenerTablasAsync(connStr);
        Assert.DoesNotContain("solicitud_idempotencias_eliminadas", tablasTrasDown, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("solicitudes", tablasTrasDown, StringComparer.OrdinalIgnoreCase);

        var indicesTrasDown = await ObtenerIndicesTablaAsync(connStr, "solicitudes");
        Assert.DoesNotContain("IX_solicitudes_fecha_entrega", indicesTrasDown, StringComparer.OrdinalIgnoreCase);

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains("solicitud_idempotencias_eliminadas", await ObtenerTablasAsync(connStr), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("IX_solicitudes_fecha_entrega", await ObtenerIndicesTablaAsync(connStr, "solicitudes"), StringComparer.OrdinalIgnoreCase);
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

    private static async Task<HashSet<string>> ObtenerColumnasTablaAsync(string connectionString, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SHOW COLUMNS FROM `{tableName}`;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<HashSet<string>> ObtenerIndicesTablaAsync(string connectionString, string tableName)
    {
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SHOW INDEX FROM `{tableName}`;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(reader.GetOrdinal("Key_name")));
        }

        return indexes;
    }

    private static async Task<string?> ObtenerCollationColumnaAsync(string connectionString, string tableName, string columnName)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SHOW FULL COLUMNS FROM `{tableName}` WHERE Field = @field;";
        cmd.Parameters.AddWithValue("@field", columnName);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var ordinal = reader.GetOrdinal("Collation");
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        return null;
    }
}
