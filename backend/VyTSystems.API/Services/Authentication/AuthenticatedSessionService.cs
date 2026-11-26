using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using VyTSystems.API.Contracts.Auth;

namespace VyTSystems.API.Services.Authentication;

internal sealed class AuthenticatedSessionService(IHttpContextAccessor accessor) : IAuthenticatedSessionService
{
    private static readonly string[] RecognizedRoles = ["Administrador", "Consulta"];

    public SessionResponse? GetCurrentSession()
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.Identity.Name;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName))
            return null;

        return new SessionResponse(userId, userName, RecognizedRoles.Where(user.IsInRole).ToArray());
    }

    public Task SignOutAsync()
    {
        var context = accessor.HttpContext
            ?? throw new InvalidOperationException("El cierre de sesión requiere una petición HTTP activa.");
        return context.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
