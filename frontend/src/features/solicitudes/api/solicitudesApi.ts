import { httpRequest, httpRequestWithHeaders } from '../../../core/api/httpClient';
import { getCsrfToken } from '../../auth/api/authApi';
import type {
  ActualizarSolicitudPayload,
  CrearSolicitudPayload,
  CrearSolicitudResponse,
  SolicitudEdicionResponse,
  SolicitudEdicionSnapshot,
  SolicitudesCatalogos,
} from '../types/solicitudes.types';

export class MissingETagError extends Error {
  constructor() {
    super('La respuesta del servidor no incluyó una versión ETag válida.');
    this.name = 'MissingETagError';
  }
}

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

export async function obtenerSolicitudParaEdicion(
  folio: string,
  signal?: AbortSignal,
): Promise<SolicitudEdicionSnapshot> {
  const { data, headers } = await httpRequestWithHeaders<SolicitudEdicionResponse>(
    `/solicitudes/edicion?folio=${encodeURIComponent(folio)}`,
    { signal },
  );

  const etag = headers.get('ETag')?.trim() ?? '';
  if (!etag) {
    throw new MissingETagError();
  }

  return { solicitud: data, etag };
}

export async function actualizarSolicitud(
  id: string,
  payload: ActualizarSolicitudPayload,
  etag: string,
): Promise<{ solicitud: SolicitudEdicionResponse; etag: string }> {
  const csrfToken = await getCsrfToken();
  const sanitizedPayload: ActualizarSolicitudPayload = {
    fechaEntrega: payload.fechaEntrega,
    horaEntrega: payload.horaEntrega,
    areaReceptoraId: payload.areaReceptoraId,
    asunto: payload.asunto,
    medioEntrega: payload.medioEntrega,
    estatusEntrega: payload.estatusEntrega,
    tipoSolicitud: payload.tipoSolicitud,
    tipoEvidencia: payload.tipoEvidencia,
    tieneAcuse: payload.tieneAcuse,
    tipoAcuse: payload.tipoAcuse,
    fechaVencimiento: payload.fechaVencimiento,
  };

  const { data, headers } = await httpRequestWithHeaders<
    SolicitudEdicionResponse,
    ActualizarSolicitudPayload
  >(`/solicitudes/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': csrfToken,
      'If-Match': etag,
    },
    body: sanitizedPayload,
  });

  const nextEtag = headers.get('ETag')?.trim() ?? '';
  return { solicitud: data, etag: nextEtag };
}
