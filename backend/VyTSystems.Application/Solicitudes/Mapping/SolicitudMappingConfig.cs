using Mapster;
using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.Mapping;

/// <summary>
/// Configuración explícita de Mapster para proyecciones de lectura y respuesta del módulo de solicitudes.
/// </summary>
public sealed class SolicitudMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AreaReceptora, AreaReceptoraCatalogoDto>();

        config.NewConfig<Solicitud, SolicitudCreadaDto>()
            .Map(dest => dest.DiasRestantes, _ => 0)
            .Map(dest => dest.Estatus, _ => string.Empty);

        config.NewConfig<Solicitud, SolicitudEdicionDto>()
            .Map(dest => dest.MedioEntrega, src => src.MedioEntrega.ToString())
            .Map(dest => dest.EstatusEntrega, src => src.EstatusEntrega.ToString())
            .Map(dest => dest.TipoSolicitud, src => src.TipoSolicitud.ToString())
            .Map(dest => dest.TipoEvidencia, src => src.TipoEvidencia.ToString())
            .Map(dest => dest.TipoAcuse, src => src.TipoAcuse.ToString())
            .Map(dest => dest.DiasRestantes, _ => 0)
            .Map(dest => dest.EstatusEfectivo, _ => string.Empty);
    }
}
