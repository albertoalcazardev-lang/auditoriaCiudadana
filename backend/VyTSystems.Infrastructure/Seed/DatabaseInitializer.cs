using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Seed;

/// <summary>
/// Inicializador de base de datos exclusivo para entorno Development.
/// Verifica conectividad con MySQL, aplica migraciones pendientes cuando existan y ejecuta IDataSeeder.
/// </summary>
public sealed class DatabaseInitializer(
    VyTSystemsDbContext dbContext,
    IEnumerable<IDataSeeder> seeders,
    IHostEnvironment hostEnvironment,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return;
        }

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("No fue posible conectar a la base de datos MySQL configurada en Development.");
            return;
        }

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
        {
            logger.LogInformation("Aplicando {Count} migración(es) pendiente(s) en MySQL...", pendingMigrations.Length);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            logger.LogInformation("Ejecutando seeder del módulo '{ModuleName}'...", seeder.ModuleName);
            await seeder.SeedAsync(dbContext, cancellationToken);
        }
    }
}

public static class DatabaseInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
