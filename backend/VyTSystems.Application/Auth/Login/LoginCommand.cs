using MediatR;
using VyTSystems.Application.Abstractions.Authentication;

namespace VyTSystems.Application.Auth.Login;

// Una clase evita que ToString() imprima las credenciales como en un record posicional.
public sealed class LoginCommand : IRequest<LoginResult>
{
    public required string Identifier { get; init; }
    public required string Password { get; init; }
    public required RequestedAccess RequestedAccess { get; init; }
    public bool RememberSession { get; init; }
}
