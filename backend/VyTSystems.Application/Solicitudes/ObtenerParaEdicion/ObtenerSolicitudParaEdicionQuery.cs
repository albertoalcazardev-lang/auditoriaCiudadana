using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

/// <summary>
/// Consulta CQRS para recuperar una solicitud por su folio junto con su versión de concurrencia vigente.
/// </summary>
public sealed record ObtenerSolicitudParaEdicionQuery(string? Folio)
    : IRequest<Result<ObtenerSolicitudParaEdicionResult>>;
