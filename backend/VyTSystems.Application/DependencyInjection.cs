using FluentValidation;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VyTSystems.Application.Common.Behaviors;
using VyTSystems.Application.Common.Mapping;

namespace VyTSystems.Application;

/// <summary>
/// Registro centralizado de dependencias de la capa Application (CQRS, FluentValidation y Mapster).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.LicenseKey = Environment.GetEnvironmentVariable("VYT_MEDIATR_LICENSE_KEY");
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        services.TryAddSingleton(_ => MappingConfig.CreateConfiguration(assembly));
        services.TryAddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
