using MapsterMapper;
using MediatR;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Catalogos;

/// <summary>
/// Handler que recupera áreas receptoras activas desde persistencia y construye los catálogos permitidos de enumeración.
/// </summary>
public sealed class ObtenerCatalogosSolicitudHandler(
    IAreaReceptoraReadRepository areaReceptoraReadRepository,
    IMapper mapper)
    : IRequestHandler<ObtenerCatalogosSolicitudQuery, Result<SolicitudesCatalogosDto>>
{
    private static readonly IReadOnlyList<CatalogoOpcionDto> MediosEntregaOpciones =
    [
        new(nameof(MedioEntrega.Digital), "Digital"),
        new(nameof(MedioEntrega.Impreso), "Impreso"),
        new(nameof(MedioEntrega.PortalWeb), "Portal web"),
        new(nameof(MedioEntrega.CorreoElectronico), "Correo electrónico")
    ];

    private static readonly IReadOnlyList<CatalogoOpcionDto> EstatusEntregaOpciones =
    [
        new(nameof(EstatusEntrega.EnProceso), "En proceso"),
        new(nameof(EstatusEntrega.Entregada), "Entregada")
    ];

    private static readonly IReadOnlyList<CatalogoOpcionDto> TiposSolicitudOpciones =
    [
        new(nameof(TipoSolicitud.Informacion), "Información"),
        new(nameof(TipoSolicitud.Aclaracion), "Aclaración"),
        new(nameof(TipoSolicitud.Otro), "Otro")
    ];

    private static readonly IReadOnlyList<CatalogoOpcionDto> TiposEvidenciaOpciones =
    [
        new(nameof(TipoEvidencia.Carpeta), "Carpeta"),
        new(nameof(TipoEvidencia.Link), "Link")
    ];

    private static readonly IReadOnlyList<CatalogoOpcionDto> TiposAcuseOpciones =
    [
        new(nameof(TipoAcuse.Sello), "Sello"),
        new(nameof(TipoAcuse.Folio), "Folio"),
        new(nameof(TipoAcuse.Electronico), "Electrónico"),
        new(nameof(TipoAcuse.NoAplica), "No aplica")
    ];

    public async Task<Result<SolicitudesCatalogosDto>> Handle(
        ObtenerCatalogosSolicitudQuery request,
        CancellationToken cancellationToken)
    {
        var lectura = await areaReceptoraReadRepository.ListarActivasAsync(cancellationToken);
        if (!lectura.Disponible)
        {
            return Result.Failure<SolicitudesCatalogosDto>(Error.Failure(
                "Persistence.Unavailable",
                "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."));
        }

        var areas = lectura.Areas
            .Select(mapper.Map<AreaReceptoraCatalogoDto>)
            .ToArray();

        return Result.Success(new SolicitudesCatalogosDto(
            areas,
            MediosEntregaOpciones,
            EstatusEntregaOpciones,
            TiposSolicitudOpciones,
            TiposEvidenciaOpciones,
            TiposAcuseOpciones));
    }
}
