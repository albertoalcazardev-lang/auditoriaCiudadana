using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MySql;
using VyTSystems.Infrastructure.Persistence;

namespace VyTSystems.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class MySqlIntegrationCollection : ICollectionFixture<MySqlTestFixture>
{
    public const string Name = "MySqlIntegration";
}

/// <summary>
/// Contenedor efímero MySQL 8.4 aislado y sembrado dinámicamente para todas las pruebas de integración.
/// </summary>
public sealed class MySqlTestFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container;

    public string ConnectionString { get; private set; } = string.Empty;
    public string AdminUserId { get; private set; } = string.Empty;
    public string AdminUserName { get; private set; } = string.Empty;
    public string AdminEmail { get; private set; } = string.Empty;
    public string AdminPassword { get; private set; } = string.Empty;

    public string ConsultaUserId { get; private set; } = string.Empty;
    public string ConsultaUserName { get; private set; } = string.Empty;
    public string ConsultaEmail { get; private set; } = string.Empty;
    public string ConsultaPassword { get; private set; } = string.Empty;

    public MySqlTestFixture()
    {
        var dbPassword = $"VyT_{Guid.NewGuid():N}!8a";
        _container = new MySqlBuilder("mysql:8.4")
            .WithDatabase($"vytsystems_test_{Guid.NewGuid():N}")
            .WithUsername("vyt_test")
            .WithPassword(dbPassword)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<VyTSystemsDbContext>(options =>
            options.UseMySQL(ConnectionString));

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._+";
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<VyTSystemsDbContext>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<VyTSystemsDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { "Administrador", "Consulta" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName)
                {
                    Id = Guid.CreateVersion7().ToString()
                });
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"No se pudo crear rol {roleName}.");
                }
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        AdminUserId = Guid.CreateVersion7().ToString();
        AdminUserName = $"admin_{Guid.NewGuid():N}"[..18];
        AdminEmail = $"{AdminUserName}@vytsystems.test";
        AdminPassword = $"Adm!{Guid.NewGuid():N}9z";
        var adminUser = new IdentityUser
        {
            Id = AdminUserId,
            UserName = AdminUserName,
            Email = AdminEmail,
            EmailConfirmed = true
        };
        var adminCreate = await userManager.CreateAsync(adminUser, AdminPassword);
        if (!adminCreate.Succeeded)
        {
            throw new InvalidOperationException("No se pudo crear usuario Administrador de pruebas.");
        }
        await userManager.AddToRoleAsync(adminUser, "Administrador");

        ConsultaUserId = Guid.CreateVersion7().ToString();
        ConsultaUserName = $"cons_{Guid.NewGuid():N}"[..18];
        ConsultaEmail = $"{ConsultaUserName}@vytsystems.test";
        ConsultaPassword = $"Con!{Guid.NewGuid():N}9z";
        var consultaUser = new IdentityUser
        {
            Id = ConsultaUserId,
            UserName = ConsultaUserName,
            Email = ConsultaEmail,
            EmailConfirmed = true
        };
        var consultaCreate = await userManager.CreateAsync(consultaUser, ConsultaPassword);
        if (!consultaCreate.Succeeded)
        {
            throw new InvalidOperationException("No se pudo crear usuario Consulta de pruebas.");
        }
        await userManager.AddToRoleAsync(consultaUser, "Consulta");
    }

    public VyTSystemsDbContext CreateDbContext(string? connectionStringOverride = null)
    {
        var options = new DbContextOptionsBuilder<VyTSystemsDbContext>()
            .UseMySQL(connectionStringOverride ?? ConnectionString)
            .Options;
        return new VyTSystemsDbContext(options);
    }

    public async Task ResetSolicitudesAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM solicitudes;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM solicitud_idempotencias_eliminadas;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM folio_secuencias;");
    }

    public VyTSystemsWebAppFactory CreateWebApplicationFactory(
        string? connectionStringOverride = null,
        string environment = "Production",
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configureTestServices = null) =>
        new(connectionStringOverride ?? ConnectionString, environment, timeProvider, configureTestServices);

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public static NullLogger<T> NullLoggerFor<T>() => NullLogger<T>.Instance;
}
