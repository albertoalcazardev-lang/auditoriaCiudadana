using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VyTSystems.Domain.Entities;
using VyTSystems.Infrastructure.Persistence.Entities;

namespace VyTSystems.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core conectado exclusivamente a MySQL.
/// Las configuraciones Fluent API se cargan automáticamente desde Persistence/Configurations.
/// </summary>
public sealed class VyTSystemsDbContext(DbContextOptions<VyTSystemsDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<AreaReceptora> AreasReceptoras => Set<AreaReceptora>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    internal DbSet<FolioSecuencia> FolioSecuencias => Set<FolioSecuencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity genera los Id como GUID en texto. EF propaga esta longitud a sus FK.
        const int idLength = 36;
        modelBuilder.Entity<IdentityUser>(user =>
        {
            user.Property(x => x.Id).HasMaxLength(idLength);
            user.Property(x => x.UserName).IsRequired();
            user.Property(x => x.NormalizedUserName).IsRequired();
            user.Property(x => x.Email).IsRequired();
            user.Property(x => x.NormalizedEmail).IsRequired();
            // Reutiliza EmailIndex de Identity. NOT NULL impide eludir UNIQUE con varios NULL.
            user.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        modelBuilder.Entity<IdentityRole>().Property(x => x.Id).HasMaxLength(idLength);

        // Limita las partes textuales de las PK compuestas para los índices InnoDB con utf8mb4.
        const int providerKeyLength = 128;
        modelBuilder.Entity<IdentityUserLogin<string>>(login =>
        {
            login.Property(x => x.LoginProvider).HasMaxLength(providerKeyLength);
            login.Property(x => x.ProviderKey).HasMaxLength(providerKeyLength);
        });
        modelBuilder.Entity<IdentityUserToken<string>>(token =>
        {
            token.Property(x => x.LoginProvider).HasMaxLength(providerKeyLength);
            token.Property(x => x.Name).HasMaxLength(providerKeyLength);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VyTSystemsDbContext).Assembly);
    }
}
