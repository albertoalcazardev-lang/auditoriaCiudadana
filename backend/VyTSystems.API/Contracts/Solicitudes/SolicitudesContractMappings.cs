using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Application.Solicitudes.Crear;

namespace VyTSystems.API.Contracts.Solicitudes;

internal static class SolicitudesContractMappings
{
    public static CrearSolicitudCommand ToCommand(
        this CrearSolicitudRequest request,
        string usuarioId,
        string? idempotencyKey) =>
        new(
            usuarioId,
            idempotencyKey,
            request.GenerarFolioAutomatico,
            request.Folio,
            request.FechaEntrega,
            request.HoraEntrega,
            request.AreaReceptoraId,
            request.Asunto,
            request.MedioEntrega,
            request.EstatusEntrega,
            request.TipoSolicitud,
            request.TipoEvidencia,
            request.TieneAcuse,
            request.TipoAcuse,
            request.FechaVencimiento);

    public static SolicitudesCatalogosResponse ToResponse(this SolicitudesCatalogosDto dto) =>
        new(
            dto.AreasReceptoras
                .Select(a => new AreaReceptoraResponse(a.Id.ToString(), a.Nombre))
                .ToArray(),
            MapearOpciones(dto.MediosEntrega),
            MapearOpciones(dto.EstatusEntrega),
            MapearOpciones(dto.TiposSolicitud),
            MapearOpciones(dto.TiposEvidencia),
            MapearOpciones(dto.TiposAcuse));

    public static CrearSolicitudResponse ToResponse(this SolicitudCreadaDto dto) =>
        new(
            dto.Id.ToString(),
            dto.Folio,
            dto.FechaVencimiento,
            dto.DiasRestantes,
            dto.Estatus);

    private static IReadOnlyList<CatalogoOpcionResponse> MapearOpciones(IReadOnlyList<CatalogoOpcionDto> opciones) =>
        opciones.Select(o => new CatalogoOpcionResponse(o.Codigo, o.Nombre)).ToArray();
}
