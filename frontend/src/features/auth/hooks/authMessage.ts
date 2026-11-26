import { HttpClientError } from '../../../core/api/httpClient';

export function authMessage(error: unknown, login = false): string {
  if (!(error instanceof HttpClientError)) return 'No se pudo comunicar con el servicio. Comprueba la conexión e inténtalo de nuevo.';
  switch (error.statusCode) {
    case 400: return 'La solicitud no es válida o caducó su protección. Revisa los campos y vuelve a intentarlo.';
    case 401: return login ? 'Usuario, contraseña o tipo de acceso incorrectos.' : 'La sesión ya no está disponible.';
    case 403: return 'No tienes permiso para este acceso.';
    case 503: return 'El servicio está temporalmente indisponible. Inténtalo más tarde.';
    default: return 'No se pudo completar la solicitud. Vuelve a intentarlo.';
  }
}
