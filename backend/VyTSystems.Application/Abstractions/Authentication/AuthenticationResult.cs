namespace VyTSystems.Application.Abstractions.Authentication;

/// <summary>Resultado interno del proveedor. No se expone directamente al cliente.</summary>
public enum AuthenticationResult
{
    ServiceUnavailable = 0,
    Authorized,
    InvalidCredentials,
    AccessDenied,
    AccountDisabledOrLocked
}
