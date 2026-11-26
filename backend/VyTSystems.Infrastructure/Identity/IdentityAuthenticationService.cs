using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VyTSystems.Application.Abstractions.Authentication;

namespace VyTSystems.Infrastructure.Identity;

public sealed class IdentityAuthenticationService(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    ILogger<IdentityAuthenticationService> logger) : IAuthenticationService
{
    public async Task<AuthenticationResult> AuthenticateAsync(
        string identifier, string password, RequestedAccess requestedAccess, bool rememberSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = requestedAccess switch
        {
            RequestedAccess.Administrador => "Administrador",
            RequestedAccess.Consulta => "Consulta",
            _ => null
        };
        if (role is null)
            return AuthenticationResult.AccessDenied;

        try
        {
            var user = identifier.Contains('@')
                ? await users.FindByEmailAsync(identifier)
                : await users.FindByNameAsync(identifier);
            cancellationToken.ThrowIfCancellationRequested();
            if (user is null)
                return AuthenticationResult.InvalidCredentials;

            var check = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (check.IsLockedOut || check.IsNotAllowed)
                return AuthenticationResult.AccountDisabledOrLocked;
            if (check.RequiresTwoFactor)
                return AuthenticationResult.AccessDenied;
            if (!check.Succeeded)
                return AuthenticationResult.InvalidCredentials;

            // CheckPasswordSignInAsync no completa el segundo factor. No emitir una cookie
            // de aplicación para una cuenta con 2FA, incluso si aún no tiene proveedor registrado.
            var requiresTwoFactor = await users.GetTwoFactorEnabledAsync(user);
            cancellationToken.ThrowIfCancellationRequested();
            if (requiresTwoFactor)
                return AuthenticationResult.AccessDenied;

            var authorized = await users.IsInRoleAsync(user, role);
            cancellationToken.ThrowIfCancellationRequested();
            if (!authorized)
                return AuthenticationResult.AccessDenied;

            await signIn.SignInAsync(user, isPersistent: rememberSession);
            return AuthenticationResult.Authorized;
        }
        catch (Exception ex) when (ex is DbException or TimeoutException or DbUpdateException { InnerException: DbException })
        {
            cancellationToken.ThrowIfCancellationRequested();
            // El mensaje del proveedor puede contener datos de la cuenta.
            var cause = ex.GetBaseException();
            logger.LogError("Fallo técnico de inicio de sesión: {Tipo}, código {Codigo}.",
                cause.GetType().Name, (cause as DbException)?.ErrorCode);
            return AuthenticationResult.ServiceUnavailable;
        }
    }
}
