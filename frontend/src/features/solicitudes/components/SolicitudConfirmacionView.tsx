import { CheckCircle2, Hash, Pencil, Plus } from 'lucide-react';
import { formatIsoToDdMmYyyy } from '../hooks/solicitudesDateUtils';
import type { SolicitudConfirmacionResumen } from '../types/solicitudes.types';

interface SolicitudConfirmacionViewProps {
  confirmacion: SolicitudConfirmacionResumen;
  mode?: 'created' | 'updated';
  onRegistrarOtra?: () => void;
  onEditarSolicitud?: () => void;
  onSeguirEditando?: () => void;
  onVolverPanel: () => void;
}

function formatEstatusLabel(estatus: string): string {
  if (estatus === 'EnProceso') return 'En proceso';
  if (estatus === 'Entregada') return 'Entregada';
  if (estatus === 'Vencida') return 'Vencida';
  return estatus;
}

export function SolicitudConfirmacionView({
  confirmacion,
  mode = 'created',
  onRegistrarOtra,
  onEditarSolicitud,
  onSeguirEditando,
  onVolverPanel,
}: SolicitudConfirmacionViewProps) {
  const { resultado } = confirmacion;
  const isUpdated = mode === 'updated';

  return (
    <section className="solicitud-card" aria-labelledby="confirmacion-titulo">
      <div
        role={isUpdated ? undefined : 'status'}
        className="solicitud-banner solicitud-banner-success"
      >
        <div className="solicitud-banner-content">
          <CheckCircle2 size={20} aria-hidden="true" />
          <span>
            {isUpdated
              ? `Solicitud ${resultado.folio} actualizada correctamente`
              : `Solicitud ${resultado.folio} registrada correctamente`}
          </span>
        </div>
      </div>

      <div className="solicitud-card-header">
        <h2 id="confirmacion-titulo" className="solicitud-card-title">
          {isUpdated ? 'Resumen de solicitud actualizada' : 'Resumen de solicitud registrada'}
        </h2>
        {isUpdated && (
          <span className="solicitud-folio-chip-immutable">
            <Hash size={14} aria-hidden="true" />
            <span>{`Folio ${resultado.folio}`}</span>
          </span>
        )}
      </div>

      <dl className="solicitud-summary-grid">
        <div className="solicitud-summary-item">
          <dt>Folio</dt>
          <dd>{resultado.folio}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Estatus</dt>
          <dd>{formatEstatusLabel(resultado.estatus)}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Plazo de vencimiento</dt>
          <dd>{formatIsoToDdMmYyyy(resultado.fechaVencimiento)}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Días restantes</dt>
          <dd>
            {resultado.diasRestantes} {resultado.diasRestantes === 1 ? 'día' : 'días'}
          </dd>
        </div>

        <div className="solicitud-summary-item solicitud-col-full">
          <dt>Área o dependencia receptora</dt>
          <dd>{confirmacion.areaReceptoraNombre}</dd>
        </div>

        <div className="solicitud-summary-item solicitud-col-full">
          <dt>Asunto corto</dt>
          <dd>{confirmacion.asunto}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Fecha y hora de entrega</dt>
          <dd>
            {confirmacion.fechaEntrega} {confirmacion.horaEntrega}
          </dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Medio de entrega</dt>
          <dd>{confirmacion.medioEntregaNombre}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Tipo de solicitud</dt>
          <dd>{confirmacion.tipoSolicitudNombre}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Evidencia que se lleva a cabo</dt>
          <dd>{confirmacion.tipoEvidenciaNombre}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>¿Tiene acuse?</dt>
          <dd>{confirmacion.tieneAcuseTexto}</dd>
        </div>

        <div className="solicitud-summary-item">
          <dt>Tipo de acuse</dt>
          <dd>{confirmacion.tipoAcuseNombre}</dd>
        </div>
      </dl>

      <div className="solicitud-actions-bar">
        <div className="solicitud-actions-buttons">
          <button
            type="button"
            className="solicitud-btn solicitud-btn-secondary"
            onClick={onVolverPanel}
          >
            Volver al panel
          </button>

          {!isUpdated && onEditarSolicitud && (
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              onClick={onEditarSolicitud}
            >
              <Pencil size={16} aria-hidden="true" />
              Editar solicitud
            </button>
          )}

          {!isUpdated && onRegistrarOtra && (
            <button
              type="button"
              className="solicitud-btn solicitud-btn-primary"
              onClick={onRegistrarOtra}
            >
              <Plus size={16} aria-hidden="true" />
              Registrar otra solicitud
            </button>
          )}

          {isUpdated && onSeguirEditando && (
            <button
              type="button"
              className="solicitud-btn solicitud-btn-primary"
              onClick={onSeguirEditando}
            >
              <Pencil size={16} aria-hidden="true" />
              Seguir editando
            </button>
          )}
        </div>
      </div>
    </section>
  );
}
