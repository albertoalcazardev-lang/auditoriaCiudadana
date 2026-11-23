using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Infrastructure.Persistence;
using VyTSystems.Infrastructure.Repositories;
using VyTSystems.Infrastructure.Seed;

namespace VyTSystems.Infrastructure;

/// <summary>
/// Registro centralizado de servicios de persistencia (Entity Framework Core + Oracle MySQL).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena 'ConnectionStrings:DefaultConnection'. " +
                "Configúrela localmente mediante 'dotnet user-secrets' o variables de entorno (nunca en código versionado).");
        }

        services.AddDbContext<VyTSystemsDbContext>(options =>
            options.UseMySQL(
                connectionString,
                mysql => mysql.MigrationsAssembly(typeof(VyTSystemsDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
