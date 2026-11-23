using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Seed;

/// <summary>
/// Contrato modular e idempotente para poblar datos iniciales reproducibles en entorno de desarrollo.
/// </summary>
public interface IDataSeeder
{
    string ModuleName { get; }

    int Order => 0;

    Task SeedAsync(VyTSystemsDbContext dbContext, CancellationToken cancellationToken = default);
}
