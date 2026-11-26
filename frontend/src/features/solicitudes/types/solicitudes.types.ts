export interface AreaReceptoraItem {
  id: string;
  nombre: string;
}

export interface CatalogoCodigoItem {
  codigo: string;
  nombre: string;
}

export interface SolicitudesCatalogos {
  areasReceptoras: AreaReceptoraItem[];
  mediosEntrega: CatalogoCodigoItem[];
  estatusEntrega: CatalogoCodigoItem[];
  tiposSolicitud: CatalogoCodigoItem[];
  tiposEvidencia: CatalogoCodigoItem[];
  tiposAcuse: CatalogoCodigoItem[];
}

export interface CrearSolicitudPayload {
  generarFolioAutomatico: boolean;
  folio: string | null;
  fechaEntrega: string;
  horaEntrega: string;
  areaReceptoraId: string;
  asunto: string;
  medioEntrega: string;
  estatusEntrega: string;
  tipoSolicitud: string;
  tipoEvidencia: string;
  tieneAcuse: boolean;
  tipoAcuse: string;
  fechaVencimiento: string;
}

export interface CrearSolicitudResponse {
  id?: string;
  folio: string;
  fechaVencimiento: string;
  diasRestantes: number;
  estatus: string;
}

export interface SolicitudEdicionResponse {
  id: string;
  folio: string;
  fechaEntrega: string; // yyyy-MM-dd
  horaEntrega: string; // HH:mm
  areaReceptoraId: string;
  asunto: string;
  medioEntrega: string;
  estatusEntrega: string;
  tipoSolicitud: string;
  tipoEvidencia: string;
  tieneAcuse: boolean;
  tipoAcuse: string;
  fechaVencimiento: string; // yyyy-MM-dd
  diasRestantes: number;
  estatusEfectivo: string;
}

export interface SolicitudEdicionSnapshot {
  solicitud: SolicitudEdicionResponse;
  etag: string;
}

export interface ActualizarSolicitudPayload {
  fechaEntrega: string; // yyyy-MM-dd
  horaEntrega: string; // HH:mm
  areaReceptoraId: string;
  asunto: string;
  medioEntrega: string;
  estatusEntrega: string;
  tipoSolicitud: string;
  tipoEvidencia: string;
  tieneAcuse: boolean;
  tipoAcuse: string;
  fechaVencimiento: string; // yyyy-MM-dd
}

export type SolicitudEditableFieldName =
  | 'fechaEntrega'
  | 'horaEntrega'
  | 'areaReceptoraId'
  | 'asunto'
  | 'medioEntrega'
  | 'estatusEntrega'
  | 'tipoSolicitud'
  | 'tipoEvidencia'
  | 'tieneAcuse'
  | 'tipoAcuse'
  | 'fechaVencimiento';

export type SolicitudFieldName = 'folio' | SolicitudEditableFieldName;

export type SolicitudEditableFieldErrors = Partial<Record<SolicitudEditableFieldName, string>>;
export type SolicitudFieldErrors = Partial<Record<SolicitudFieldName, string>>;

export interface SolicitudEditableFormValues {
  fechaEntrega: string;
  horaEntrega: string;
  areaReceptoraId: string;
  asunto: string;
  medioEntrega: string;
  estatusEntrega: string;
  tipoSolicitud: string;
  tipoEvidencia: string;
  tieneAcuse: '' | 'si' | 'no';
  tipoAcuse: string;
  fechaVencimiento: string;
}

export interface SolicitudFormValues extends SolicitudEditableFormValues {
  generarFolioAutomatico: boolean;
  folio: string;
}

export type DiasRestantesTone = 'normal' | 'warning' | 'urgent' | 'empty';

export type SubmitStatus = 'idle' | 'confirming' | 'submitting' | 'uncertain' | 'succeeded';

export type SolicitudModalState =
  | 'none'
  | 'confirm-register'
  | 'confirm-discard'
  | 'confirm-uncertain-exit';

export type EditarSolicitudModalState =
  | 'none'
  | 'confirm-save'
  | 'confirm-discard'
  | 'confirm-uncertain-exit'
  | 'conflict-concurrency'
  | 'not-found';

export interface SolicitudToastMessage {
  id: number;
  tone: 'info' | 'success';
  text: string;
}

export interface SolicitudConfirmacionResumen {
  resultado: CrearSolicitudResponse;
  areaReceptoraNombre: string;
  asunto: string;
  fechaEntrega: string;
  horaEntrega: string;
  medioEntregaNombre: string;
  tipoSolicitudNombre: string;
  tipoEvidenciaNombre: string;
  tieneAcuseTexto: string;
  tipoAcuseNombre: string;
}

export type SolicitudSortField = 'vencimiento' | 'folio' | 'entrega' | 'dias';

export type SolicitudSortDirection = 'asc' | 'desc';

export type SolicitudPageSize = 10 | 25 | 50;

export type SolicitudDiasFilter = 'todos' | '0' | '1-3' | '4-7' | '8+' | '1-7' | 'custom';

export interface ListarSolicitudesParams {
  q?: string;
  ent_d?: string;
  ent_h?: string;
  ven_d?: string;
  ven_h?: string;
  dias?: Exclude<SolicitudDiasFilter, 'todos'>;
  dias_min?: number;
  dias_max?: number;
  sort?: SolicitudSortField;
  dir?: SolicitudSortDirection;
  page?: number;
  size?: SolicitudPageSize;
}

export interface SolicitudListadoItem {
  id: string;
  folio: string;
  asunto: string;
  areaReceptora: string;
  medioEntrega: string;
  estatusEfectivo: string;
  fechaEntrega: string; // yyyy-MM-dd
  fechaVencimiento: string; // yyyy-MM-dd
  diasRestantes: number;
  version: string; // token opaco de concurrencia
}

export interface ListadoSolicitudesResponse {
  items: SolicitudListadoItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface SolicitudesUrlQueryState {
  q: string;
  ent_d: string;
  ent_h: string;
  ven_d: string;
  ven_h: string;
  dias: SolicitudDiasFilter;
  dias_min?: number;
  dias_max?: number;
  sort: SolicitudSortField;
  dir: SolicitudSortDirection;
  page: number;
  size: SolicitudPageSize;
}

export interface SolicitudesFilterDraft {
  entDesde: string;
  entHasta: string;
  venDesde: string;
  venHasta: string;
  dias: SolicitudDiasFilter;
  diasMin: string;
  diasMax: string;
}

export interface SolicitudesFilterErrors {
  entDesde?: string;
  entHasta?: string;
  entRange?: string;
  venDesde?: string;
  venHasta?: string;
  venRange?: string;
  diasCustom?: string;
}

export type SolicitudFilterChipKey = 'entrega' | 'vencimiento' | 'dias';

export interface SolicitudActiveChip {
  key: SolicitudFilterChipKey;
  label: string;
}

export interface DeleteFeedbackBanner {
  tone: 'error' | 'warning' | 'info';
  message: string;
  canRefreshList: boolean;
}
