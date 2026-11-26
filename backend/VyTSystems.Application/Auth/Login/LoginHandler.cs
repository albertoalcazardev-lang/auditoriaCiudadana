using MediatR;
using VyTSystems.Application.Abstractions.Authentication;

namespace VyTSystems.Application.Auth.Login;

public sealed class LoginHandler(IAuthenticationService authentication)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await authentication.AuthenticateAsync(
            request.Identifier.Trim(), request.Password, request.RequestedAccess,
            request.RememberSession, cancellationToken);

        return result switch
        {
            AuthenticationResult.Authorized => LoginResult.Authorized,
            AuthenticationResult.InvalidCredentials or AuthenticationResult.AccessDenied
                or AuthenticationResult.AccountDisabledOrLocked => LoginResult.Rejected,
            _ => LoginResult.ServiceUnavailable
        };
    }
}
