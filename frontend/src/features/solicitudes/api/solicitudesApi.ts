import {
  HttpClientError,
  httpRequest,
  httpRequestWithHeaders,
} from '../../../core/api/httpClient';
import { getCsrfToken } from '../../auth/api/authApi';
import type {
  ActualizarSolicitudPayload,
  CrearSolicitudPayload,
  CrearSolicitudResponse,
  ListadoSolicitudesResponse,
  ListarSolicitudesParams,
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

export class DeleteResponseLostError extends Error {
  public readonly causeError: unknown;

  constructor(causeError: unknown) {
    super('Se perdió la respuesta de red durante la eliminación de la solicitud.');
    this.name = 'DeleteResponseLostError';
    this.causeError = causeError;
  }
}

export function formatStrongETag(version: string): string {
  const trimmed = version.trim();
  if (!trimmed) return '""';
  if (trimmed.startsWith('W/"') && trimmed.endsWith('"')) {
    return trimmed;
  }
  if (trimmed.startsWith('"') && trimmed.endsWith('"') && trimmed.length >= 2) {
    return trimmed;
  }
  return `"${trimmed.replace(/^"+|"+$/g, '')}"`;
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

export async function listarSolicitudes(
  params: ListarSolicitudesParams = {},
  signal?: AbortSignal,
): Promise<ListadoSolicitudesResponse> {
  const searchParams = new URLSearchParams();

  const trimmedQ = params.q?.trim();
  if (trimmedQ) {
    searchParams.set('q', trimmedQ);
  }
  if (params.ent_d?.trim()) {
    searchParams.set('ent_d', params.ent_d.trim());
  }
  if (params.ent_h?.trim()) {
    searchParams.set('ent_h', params.ent_h.trim());
  }
  if (params.ven_d?.trim()) {
    searchParams.set('ven_d', params.ven_d.trim());
  }
  if (params.ven_h?.trim()) {
    searchParams.set('ven_h', params.ven_h.trim());
  }
  if (params.dias) {
    searchParams.set('dias', params.dias);
    if (params.dias === 'custom') {
      if (params.dias_min !== undefined) {
        searchParams.set('dias_min', String(params.dias_min));
      }
      if (params.dias_max !== undefined) {
        searchParams.set('dias_max', String(params.dias_max));
      }
    }
  }

  searchParams.set('sort', params.sort ?? 'vencimiento');
  searchParams.set('dir', params.dir ?? 'asc');
  searchParams.set('page', String(params.page ?? 1));
  searchParams.set('size', String(params.size ?? 10));

  const qs = searchParams.toString();
  return httpRequest<ListadoSolicitudesResponse>(qs ? `/solicitudes?${qs}` : '/solicitudes', {
    signal,
  });
}

export async function eliminarSolicitud(id: string, version: string): Promise<void> {
  const csrfToken = await getCsrfToken();
  try {
    await httpRequest<void>(`/solicitudes/${encodeURIComponent(id)}`, {
      method: 'DELETE',
      expectedStatus: 204,
      headers: {
        'X-CSRF-TOKEN': csrfToken,
        'If-Match': formatStrongETag(version),
      },
    });
  } catch (error) {
    if (error instanceof HttpClientError) {
      throw error;
    }
    throw new DeleteResponseLostError(error);
  }
}
