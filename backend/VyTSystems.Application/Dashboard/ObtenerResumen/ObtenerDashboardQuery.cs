using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Dashboard.ObtenerResumen;

/// <summary>
/// Consulta CQRS para obtener el resumen ejecutivo del dashboard administrativo según el periodo seleccionado.
/// </summary>
public sealed record ObtenerDashboardQuery(string? Periodo) : IRequest<Result<DashboardResumenDto>>;
