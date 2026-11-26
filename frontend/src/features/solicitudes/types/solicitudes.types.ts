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
