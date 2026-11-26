import { httpRequest } from '../../../core/api/httpClient';
import { getCsrfToken } from '../../auth/api/authApi';
import type {
  CrearSolicitudPayload,
  CrearSolicitudResponse,
  SolicitudesCatalogos,
} from '../types/solicitudes.types';

export async function fetchSolicitudesCatalogos(
  signal?: AbortSignal,
): Promise<SolicitudesCatalogos> {
  return httpRequest<SolicitudesCatalogos>('/solicitudes/catalogos', { signal });
}

export async function crearSolicitud(
  payload: CrearSolicitudPayload,
  idempotencyKey: string,
): Promise<CrearSolicitudResponse> {
  const csrfToken = await getCsrfToken();
  return httpRequest<CrearSolicitudResponse, CrearSolicitudPayload>('/solicitudes', {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': csrfToken,
      'Idempotency-Key': idempotencyKey,
    },
    body: payload,
  });
}
