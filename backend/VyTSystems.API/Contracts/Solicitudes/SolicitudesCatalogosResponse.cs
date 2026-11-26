namespace VyTSystems.API.Contracts.Solicitudes;

public sealed record AreaReceptoraResponse(string Id, string Nombre);

public sealed record CatalogoOpcionResponse(string Codigo, string Nombre);

public sealed record SolicitudesCatalogosResponse(
    IReadOnlyList<AreaReceptoraResponse> AreasReceptoras,
    IReadOnlyList<CatalogoOpcionResponse> MediosEntrega,
    IReadOnlyList<CatalogoOpcionResponse> EstatusEntrega,
    IReadOnlyList<CatalogoOpcionResponse> TiposSolicitud,
    IReadOnlyList<CatalogoOpcionResponse> TiposEvidencia,
    IReadOnlyList<CatalogoOpcionResponse> TiposAcuse);
