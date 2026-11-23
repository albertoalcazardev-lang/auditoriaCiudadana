using System.Reflection;
using Mapster;

namespace VyTSystems.Application.Common.Mapping;

/// <summary>
/// Configuración compartida de Mapster para escanear perfiles IRegister de cada módulo en Application.
/// </summary>
public static class MappingConfig
{
    public static TypeAdapterConfig CreateConfiguration(Assembly applicationAssembly)
    {
        var config = new TypeAdapterConfig();
        config.Scan(applicationAssembly);
        return config;
    }
}
