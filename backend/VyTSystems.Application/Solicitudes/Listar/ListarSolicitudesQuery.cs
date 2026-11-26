using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.Listar;

/// <summary>
/// Consulta CQRS para obtener el listado paginado, filtrado y ordenado de solicitudes registradas.
/// </summary>
public sealed record ListarSolicitudesQuery(
    string? Q = null,
    string? EntD = null,
    string? EntH = null,
    string? VenD = null,
    string? VenH = null,
    string? Dias = null,
    string? DiasMin = null,
    string? DiasMax = null,
    string? Sort = null,
    string? Dir = null,
    string? Page = null,
    string? Size = null) : IRequest<Result<ListarSolicitudesResult>>;
