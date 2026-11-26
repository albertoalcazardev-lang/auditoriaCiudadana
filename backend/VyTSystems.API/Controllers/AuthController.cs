using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VyTSystems.API.Contracts.Auth;
using VyTSystems.API.Filters;
using VyTSystems.API.Services.Authentication;
using VyTSystems.Application.Auth.Login;

namespace VyTSystems.API.Controllers;

[ApiController]
[Route("api/auth")]
[NoStore]
public sealed class AuthController(ISender sender, IAuthenticatedSessionService sessions) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginCommand
        {
            Identifier = request.Identifier ?? string.Empty,
            Password = request.Password ?? string.Empty,
            RememberSession = request.RememberSession,
            RequestedAccess = request.RequestedAccess
        }, cancellationToken);

        return result switch
        {
            LoginResult.Authorized => Ok(),
            LoginResult.Rejected => Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Usuario o contraseña incorrectos. Verifica tus datos e inténtalo de nuevo."),
            LoginResult.ServiceUnavailable => Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "El servicio de autenticación no está disponible. Inténtalo de nuevo."),
            _ => throw new InvalidOperationException("Resultado de login no reconocido.")
        };
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<SessionResponse> Me()
    {
        var session = sessions.GetCurrentSession();
        return session is null ? Unauthorized() : Ok(session);
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await sessions.SignOutAsync();
        return NoContent();
    }
}
