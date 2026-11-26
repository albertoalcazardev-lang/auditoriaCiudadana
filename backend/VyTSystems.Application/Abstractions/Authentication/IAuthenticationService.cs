namespace VyTSystems.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    /// <summary>
    /// Verifica las credenciales, el estado de la cuenta y el permiso real para el modo solicitado.
    /// Recordar sesión es una preferencia, no una autorización para emitir una sesión.
    /// Los fallos operativos previstos se indican con ServiceUnavailable; la cancelación se propaga.
    /// </summary>
    /// <returns>
    /// <see cref="AuthenticationResult.Authorized"/> indica que se verificaron las credenciales,
    /// el estado de la cuenta y el acceso solicitado, y que Identity completó satisfactoriamente
    /// la emisión de la cookie de autenticación. No garantiza que el navegador haya recibido
    /// la respuesta HTTP.
    /// </returns>
    Task<AuthenticationResult> AuthenticateAsync(
        string identifier, string password, RequestedAccess requestedAccess, bool rememberSession,
        CancellationToken cancellationToken);
}
