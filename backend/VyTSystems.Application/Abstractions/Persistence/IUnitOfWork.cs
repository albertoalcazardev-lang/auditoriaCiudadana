namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Abstracción para confirmar cambios transaccionales sin acoplar Application a Entity Framework Core.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
