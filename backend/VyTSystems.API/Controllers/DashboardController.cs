using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VyTSystems.API.Contracts.Dashboard;
using VyTSystems.API.Extensions;
using VyTSystems.API.Filters;

namespace VyTSystems.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Administrador")]
[NoStore]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Obtener(
        [FromQuery] string? periodo = null,
        CancellationToken cancellationToken = default)
    {
        _ = periodo;
        var query = DashboardContractMappings.ToQuery(Request.Query["periodo"]);
        var result = await sender.Send(query, cancellationToken);
        return this.ToActionResult(result, dto => dto.ToResponse());
    }
}
