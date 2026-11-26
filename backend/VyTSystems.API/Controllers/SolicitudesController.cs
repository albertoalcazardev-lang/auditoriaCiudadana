using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VyTSystems.API.Contracts.Solicitudes;
using VyTSystems.API.Extensions;
using VyTSystems.API.Filters;
using VyTSystems.API.Services.Authentication;
using VyTSystems.Application.Solicitudes.Catalogos;

namespace VyTSystems.API.Controllers;

[ApiController]
[Route("api/solicitudes")]
[Authorize(Roles = "Administrador")]
[NoStore]
public sealed class SolicitudesController(ISender sender, IAuthenticatedSessionService sessions) : ControllerBase
{
    [HttpGet("catalogos")]
    [ProducesResponseType<SolicitudesCatalogosResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObtenerCatalogos(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ObtenerCatalogosSolicitudQuery(), cancellationToken);
        return this.ToActionResult(result, dto => dto.ToResponse());
    }

    [HttpGet("edicion")]
    [ProducesResponseType<SolicitudEdicionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObtenerParaEdicion(
        [FromQuery] string? folio,
        CancellationToken cancellationToken)
    {
        var folioValues = Request.Query["folio"];
        var query = SolicitudesContractMappings.ToEdicionQuery(folio, folioValues.Count);
        var result = await sender.Send(query, cancellationToken);
        return this.ToActionResultWithETag(
            result,
            r => r.Solicitud.ToEdicionResponse(),
            r => SolicitudesContractMappings.FormatStrongETag(r.VersionConcurrencia));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<CrearSolicitudResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CrearSolicitudResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Crear(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CrearSolicitudRequest request,
        CancellationToken cancellationToken)
    {
        var session = sessions.GetCurrentSession();
        if (session is null)
        {
            return this.UnauthorizedProblem();
        }

        var result = await sender.Send(request.ToCommand(session.UserId, idempotencyKey), cancellationToken);
        return this.ToCreatedOrOkResult(
            result,
            r => r.Solicitud.ToResponse(),
            r => r.FueRecuperadaPorIdempotencia);
    }

    [HttpPut("{id}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<SolicitudEdicionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Actualizar(
        [FromRoute] string? id,
        [FromBody] ActualizarSolicitudRequest request,
        CancellationToken cancellationToken)
    {
        var session = sessions.GetCurrentSession();
        if (session is null)
        {
            return this.UnauthorizedProblem();
        }

        var command = request.ToCommand(session.UserId, id, Request.Headers.IfMatch);
        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResultWithETag(
            result,
            r => r.Solicitud.ToEdicionResponse(),
            r => SolicitudesContractMappings.FormatStrongETag(r.VersionConcurrencia));
    }
}
