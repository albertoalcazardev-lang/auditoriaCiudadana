using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.Catalogos;

/// <summary>
/// Consulta para obtener los catálogos activos requeridos por el registro de solicitudes.
/// </summary>
public sealed record ObtenerCatalogosSolicitudQuery : IRequest<Result<SolicitudesCatalogosDto>>;
