using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Implementación transaccional de IUnitOfWork respaldada por VyTSystemsDbContext.
/// </summary>
public sealed class UnitOfWork(VyTSystemsDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
