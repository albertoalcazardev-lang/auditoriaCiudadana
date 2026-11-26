import type {
  ActualizarSolicitudPayload,
  CatalogoCodigoItem,
  CrearSolicitudResponse,
  SolicitudConfirmacionResumen,
  SolicitudEdicionResponse,
  SolicitudEditableFieldErrors,
  SolicitudEditableFieldName,
  SolicitudEditableFormValues,
  SolicitudFieldName,
  SolicitudesCatalogos,
} from '../types/solicitudes.types';
import {
  compareCalendarDates,
  formatIsoToDdMmYyyy,
  isValidTime24h,
  parseDdMmYyyy,
  toIsoDateString,
} from './solicitudesDateUtils';

export const EDITABLE_FIELD_ORDER: readonly SolicitudEditableFieldName[] = [
  'fechaEntrega',
  'horaEntrega',
  'areaReceptoraId',
  'asunto',
  'medioEntrega',
  'estatusEntrega',
  'tipoSolicitud',
  'tipoEvidencia',
  'tieneAcuse',
  'tipoAcuse',
  'fechaVencimiento',
];

export const CREATE_FIELD_ORDER: readonly SolicitudFieldName[] = [
  'folio',
  ...EDITABLE_FIELD_ORDER,
];

export const EDITABLE_SERVER_FIELD_MAP: Record<string, SolicitudEditableFieldName> = {
  fechaentrega: 'fechaEntrega',
  horaentrega: 'horaEntrega',
  areareceptoraid: 'areaReceptoraId',
  asunto: 'asunto',
  medioentrega: 'medioEntrega',
  estatusentrega: 'estatusEntrega',
  tiposolicitud: 'tipoSolicitud',
  tipoevidencia: 'tipoEvidencia',
  tieneacuse: 'tieneAcuse',
  tipoacuse: 'tipoAcuse',
  fechavencimiento: 'fechaVencimiento',
};

export const CREATE_SERVER_FIELD_MAP: Record<string, SolicitudFieldName> = {
  folio: 'folio',
  ...EDITABLE_SERVER_FIELD_MAP,
};

export function validateEditableSolicitudFields(
  values: SolicitudEditableFormValues,
  catalogos?: SolicitudesCatalogos,
): SolicitudEditableFieldErrors {
  const errors: SolicitudEditableFieldErrors = {};

  const parsedEntrega = parseDdMmYyyy(values.fechaEntrega);
  if (!values.fechaEntrega.trim()) {
    errors.fechaEntrega = 'Ingresa la fecha de entrega.';
  } else if (!parsedEntrega) {
    errors.fechaEntrega = 'Ingresa una fecha válida con formato dd/mm/aaaa.';
  }

  if (!values.horaEntrega.trim()) {
    errors.horaEntrega = 'Ingresa la hora de entrega.';
  } else if (!isValidTime24h(values.horaEntrega)) {
    errors.horaEntrega = 'Ingresa una hora válida en formato 24 h (HH:mm).';
  }

  if (
    !values.areaReceptoraId ||
    (catalogos && !catalogos.areasReceptoras.some((area) => area.id === values.areaReceptoraId))
  ) {
    errors.areaReceptoraId = 'Selecciona el área o dependencia receptora.';
  }

  if (!values.asunto.trim()) {
    errors.asunto = 'Ingresa el asunto corto de la solicitud.';
  } else if (values.asunto.length > 100) {
    errors.asunto = 'El asunto corto no puede exceder 100 caracteres.';
  }

  if (
    !values.medioEntrega ||
    (catalogos && !catalogos.mediosEntrega.some((item) => item.codigo === values.medioEntrega))
  ) {
    errors.medioEntrega = 'Selecciona el medio de entrega.';
  }

  if (
    !values.estatusEntrega ||
    values.estatusEntrega === 'Vencida' ||
    (catalogos && !catalogos.estatusEntrega.some((item) => item.codigo === values.estatusEntrega))
  ) {
    errors.estatusEntrega = 'Selecciona el estatus de entrega.';
  }

  if (
    !values.tipoSolicitud ||
    (catalogos && !catalogos.tiposSolicitud.some((item) => item.codigo === values.tipoSolicitud))
  ) {
    errors.tipoSolicitud = 'Selecciona el tipo de solicitud.';
  }

  if (
    !values.tipoEvidencia ||
    (catalogos && !catalogos.tiposEvidencia.some((item) => item.codigo === values.tipoEvidencia))
  ) {
    errors.tipoEvidencia = 'Selecciona el tipo de evidencia.';
  }

  if (values.tieneAcuse !== 'si' && values.tieneAcuse !== 'no') {
    errors.tieneAcuse = 'Indica si cuenta con acuse.';
  }

  if (
    values.tieneAcuse === 'si' &&
    (!values.tipoAcuse ||
      values.tipoAcuse === 'NoAplica' ||
      (catalogos && !catalogos.tiposAcuse.some((item) => item.codigo === values.tipoAcuse)))
  ) {
    errors.tipoAcuse = 'Selecciona un tipo de acuse válido.';
  }

  const parsedVencimiento = parseDdMmYyyy(values.fechaVencimiento);
  if (!values.fechaVencimiento.trim()) {
    errors.fechaVencimiento = 'Ingresa el plazo de vencimiento.';
  } else if (!parsedVencimiento) {
    errors.fechaVencimiento = 'Ingresa una fecha válida con formato dd/mm/aaaa.';
  } else if (parsedEntrega && compareCalendarDates(parsedVencimiento, parsedEntrega) < 0) {
    errors.fechaVencimiento = 'El plazo de vencimiento no puede ser anterior a la fecha de entrega.';
  }

  return errors;
}

export function mapValidationProblemDetails<TField extends string>(
  details: unknown,
  fieldMap: Record<string, TField>,
): Partial<Record<TField, string>> {
  if (!details || typeof details !== 'object') return {};
  const maybeErrors = (details as { errors?: unknown }).errors;
  if (!maybeErrors || typeof maybeErrors !== 'object') return {};

  const mapped: Partial<Record<TField, string>> = {};
  for (const [rawKey, rawMessages] of Object.entries(maybeErrors as Record<string, unknown>)) {
    const normalized = rawKey.trim().replace(/^\$\./, '').toLowerCase();
    const fieldName = fieldMap[normalized];
    if (!fieldName) continue;
    if (Array.isArray(rawMessages) && typeof rawMessages[0] === 'string' && rawMessages[0].trim()) {
      mapped[fieldName] = rawMessages[0];
    }
  }
  return mapped;
}

export function getSelectableEstatusEntrega(
  catalogos?: SolicitudesCatalogos,
): CatalogoCodigoItem[] {
  return (catalogos?.estatusEntrega ?? []).filter((item) => item.codigo !== 'Vencida');
}

export function getSelectableTiposAcuse(
  catalogos: SolicitudesCatalogos | undefined,
  tieneAcuse: '' | 'si' | 'no',
): CatalogoCodigoItem[] {
  const raw = catalogos?.tiposAcuse ?? [];
  if (tieneAcuse === 'no') {
    const hasNoAplica = raw.some((item) => item.codigo === 'NoAplica');
    return hasNoAplica ? raw : [...raw, { codigo: 'NoAplica', nombre: 'No aplica' }];
  }
  if (tieneAcuse === 'si') {
    return raw.filter((item) => item.codigo !== 'NoAplica');
  }
  return [];
}

export function applyTieneAcuseChange<T extends SolicitudEditableFormValues>(
  values: T,
  nextValue: '' | 'si' | 'no',
): T {
  if (nextValue === 'no') {
    return { ...values, tieneAcuse: 'no', tipoAcuse: 'NoAplica' };
  }
  if (nextValue === 'si') {
    return {
      ...values,
      tieneAcuse: 'si',
      tipoAcuse: values.tipoAcuse === 'NoAplica' ? '' : values.tipoAcuse,
    };
  }
  return { ...values, tieneAcuse: '', tipoAcuse: '' };
}

export function buildActualizarSolicitudPayload(
  values: SolicitudEditableFormValues,
): ActualizarSolicitudPayload | null {
  const parsedEntrega = parseDdMmYyyy(values.fechaEntrega);
  const parsedVencimiento = parseDdMmYyyy(values.fechaVencimiento);
  if (!parsedEntrega || !parsedVencimiento) return null;

  return {
    fechaEntrega: toIsoDateString(parsedEntrega),
    horaEntrega: values.horaEntrega.trim(),
    areaReceptoraId: values.areaReceptoraId,
    asunto: values.asunto.trim(),
    medioEntrega: values.medioEntrega,
    estatusEntrega: values.estatusEntrega,
    tipoSolicitud: values.tipoSolicitud,
    tipoEvidencia: values.tipoEvidencia,
    tieneAcuse: values.tieneAcuse === 'si',
    tipoAcuse: values.tieneAcuse === 'no' ? 'NoAplica' : values.tipoAcuse,
    fechaVencimiento: toIsoDateString(parsedVencimiento),
  };
}

export function mapSolicitudEdicionToFormValues(
  solicitud: SolicitudEdicionResponse,
): SolicitudEditableFormValues {
  const tieneAcuse: 'si' | 'no' = solicitud.tieneAcuse ? 'si' : 'no';
  return {
    fechaEntrega: formatIsoToDdMmYyyy(solicitud.fechaEntrega),
    horaEntrega: solicitud.horaEntrega.trim(),
    areaReceptoraId: solicitud.areaReceptoraId,
    asunto: solicitud.asunto,
    medioEntrega: solicitud.medioEntrega,
    estatusEntrega: solicitud.estatusEntrega,
    tipoSolicitud: solicitud.tipoSolicitud,
    tipoEvidencia: solicitud.tipoEvidencia,
    tieneAcuse,
    tipoAcuse: tieneAcuse === 'no' ? 'NoAplica' : solicitud.tipoAcuse,
    fechaVencimiento: formatIsoToDdMmYyyy(solicitud.fechaVencimiento),
  };
}

export function areEditableSolicitudValuesEquivalent(
  current: SolicitudEditableFormValues,
  baseline: SolicitudEditableFormValues,
): boolean {
  const currentAcuseCode = current.tieneAcuse === 'no' ? 'NoAplica' : current.tipoAcuse.trim();
  const baseAcuseCode = baseline.tieneAcuse === 'no' ? 'NoAplica' : baseline.tipoAcuse.trim();

  return (
    current.fechaEntrega.trim() === baseline.fechaEntrega.trim() &&
    current.horaEntrega.trim() === baseline.horaEntrega.trim() &&
    current.areaReceptoraId === baseline.areaReceptoraId &&
    current.asunto.trim() === baseline.asunto.trim() &&
    current.medioEntrega === baseline.medioEntrega &&
    current.estatusEntrega === baseline.estatusEntrega &&
    current.tipoSolicitud === baseline.tipoSolicitud &&
    current.tipoEvidencia === baseline.tipoEvidencia &&
    current.tieneAcuse === baseline.tieneAcuse &&
    currentAcuseCode === baseAcuseCode &&
    current.fechaVencimiento.trim() === baseline.fechaVencimiento.trim()
  );
}

export function buildSolicitudConfirmacionResumen(
  resultado: CrearSolicitudResponse,
  values: SolicitudEditableFormValues,
  catalogos?: SolicitudesCatalogos,
): SolicitudConfirmacionResumen {
  const areaNombre =
    catalogos?.areasReceptoras.find((a) => a.id === values.areaReceptoraId)?.nombre ??
    values.areaReceptoraId;
  const medioNombre =
    catalogos?.mediosEntrega.find((m) => m.codigo === values.medioEntrega)?.nombre ??
    values.medioEntrega;
  const tipoNombre =
    catalogos?.tiposSolicitud.find((t) => t.codigo === values.tipoSolicitud)?.nombre ??
    values.tipoSolicitud;
  const evidenciaNombre =
    catalogos?.tiposEvidencia.find((e) => e.codigo === values.tipoEvidencia)?.nombre ??
    values.tipoEvidencia;
  const tipoAcuseNombre =
    values.tieneAcuse === 'no'
      ? 'No aplica'
      : (catalogos?.tiposAcuse.find((a) => a.codigo === values.tipoAcuse)?.nombre ??
        values.tipoAcuse);

  return {
    resultado,
    areaReceptoraNombre: areaNombre,
    asunto: values.asunto.trim(),
    fechaEntrega: values.fechaEntrega.trim(),
    horaEntrega: values.horaEntrega.trim(),
    medioEntregaNombre: medioNombre,
    tipoSolicitudNombre: tipoNombre,
    tipoEvidenciaNombre: evidenciaNombre,
    tieneAcuseTexto: values.tieneAcuse === 'si' ? 'Sí' : 'No',
    tipoAcuseNombre,
  };
}
