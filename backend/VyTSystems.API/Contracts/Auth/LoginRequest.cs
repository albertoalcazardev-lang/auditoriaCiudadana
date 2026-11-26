using System.Text.Json.Serialization;
using VyTSystems.Application.Abstractions.Authentication;

namespace VyTSystems.API.Contracts.Auth;

public sealed class LoginRequest
{
    public string? Identifier { get; init; }
    public string? Password { get; init; }
    public bool RememberSession { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<RequestedAccess>))]
    public RequestedAccess RequestedAccess { get; init; }
}
