using System.Reflection;
using FluentValidation;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application.Common.CQRS;
using VyTSystems.Application.Common.Mapping;

namespace VyTSystems.Application;

/// <summary>
/// Registro centralizado de dependencias de la capa Application (CQRS, FluentValidation y Mapster).
/// </summary>
public static class DependencyInjection
{
    private static readonly Type[] HandlerOpenGenericTypes =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<RequestDispatcher>();
        services.AddScoped<ICommandDispatcher>(sp => sp.GetRequiredService<RequestDispatcher>());
        services.AddScoped<IQueryDispatcher>(sp => sp.GetRequiredService<RequestDispatcher>());

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        var mappingConfig = MappingConfig.CreateConfiguration(assembly);
        services.AddSingleton(mappingConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        RegisterHandlersFromAssembly(services, assembly);

        return services;
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var concreteTypes = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                           && type != typeof(RequestDispatcher));

        foreach (var implementationType in concreteTypes)
        {
            var handlerInterfaces = implementationType
                .GetInterfaces()
                .Where(iface => iface.IsGenericType
                                && HandlerOpenGenericTypes.Contains(iface.GetGenericTypeDefinition()));

            foreach (var serviceInterface in handlerInterfaces)
            {
                services.AddScoped(serviceInterface, implementationType);
            }
        }
    }
}
