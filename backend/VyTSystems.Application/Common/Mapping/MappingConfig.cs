using System.Reflection;
using Mapster;

namespace VyTSystems.Application.Common.Mapping;

/// <summary>
/// Configuración común de Mapster encargada de construir una instancia aislada de TypeAdapterConfig
/// y descubrir automáticamente las implementaciones de IRegister en los módulos de Application.
/// </summary>
public static class MappingConfig
{
    public static TypeAdapterConfig CreateConfiguration(Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        var config = new TypeAdapterConfig();
        config.Scan(applicationAssembly);

        return config;
    }
}
