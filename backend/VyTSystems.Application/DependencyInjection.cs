using System.Reflection;
using FluentValidation;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.TryAddScoped<RequestDispatcher>();
        services.TryAddScoped<ICommandDispatcher>(sp => sp.GetRequiredService<RequestDispatcher>());
        services.TryAddScoped<IQueryDispatcher>(sp => sp.GetRequiredService<RequestDispatcher>());

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        services.TryAddSingleton(_ => MappingConfig.CreateConfiguration(assembly));
        services.TryAddScoped<IMapper, ServiceMapper>();

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
                services.TryAddScoped(serviceInterface, implementationType);
            }
        }
    }
}
