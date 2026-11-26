using VyTSystems.API.Contracts.Auth;

namespace VyTSystems.API.Services.Authentication;

public interface IAuthenticatedSessionService
{
    SessionResponse? GetCurrentSession();
    Task SignOutAsync();
}
