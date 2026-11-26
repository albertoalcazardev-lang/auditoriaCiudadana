namespace VyTSystems.API.Contracts.Auth;

public sealed record SessionResponse(string UserId, string UserName, string[] Roles);
