namespace VyTSystems.Application.Solicitudes.Catalogos;

/// <summary>
/// Elemento de catálogo de área receptora activa.
/// </summary>
public sealed record AreaReceptoraCatalogoDto(Guid Id, string Nombre);

/// <summary>
/// Opción serializable de catálogo de enumeración con código técnico y etiqueta legible.
/// </summary>
public sealed record CatalogoOpcionDto(string Codigo, string Nombre);

/// <summary>
/// Conjunto completo de catálogos necesarios para capturar una nueva solicitud.
/// </summary>
public sealed record SolicitudesCatalogosDto(
    IReadOnlyList<AreaReceptoraCatalogoDto> AreasReceptoras,
    IReadOnlyList<CatalogoOpcionDto> MediosEntrega,
    IReadOnlyList<CatalogoOpcionDto> EstatusEntrega,
    IReadOnlyList<CatalogoOpcionDto> TiposSolicitud,
    IReadOnlyList<CatalogoOpcionDto> TiposEvidencia,
    IReadOnlyList<CatalogoOpcionDto> TiposAcuse);
