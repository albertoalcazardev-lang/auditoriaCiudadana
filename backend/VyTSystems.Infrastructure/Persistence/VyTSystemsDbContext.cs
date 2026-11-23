using Microsoft.EntityFrameworkCore;

namespace VyTSystems.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core conectado exclusivamente a MySQL.
/// Las configuraciones Fluent API se cargan automáticamente desde Persistence/Configurations.
/// </summary>
public sealed class VyTSystemsDbContext(DbContextOptions<VyTSystemsDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VyTSystemsDbContext).Assembly);
    }
}
