namespace VyTSystems.Application.Auth.Login;

/// <summary>Resultado del caso de uso, sin sesión ni detalles que identifiquen una cuenta.</summary>
public enum LoginResult
{
    ServiceUnavailable = 0,
    Authorized,
    Rejected
}
