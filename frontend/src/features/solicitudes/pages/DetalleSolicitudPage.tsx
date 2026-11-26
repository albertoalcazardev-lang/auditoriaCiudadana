import { AlertCircle, ArrowLeft, FileSearch, Hash, RefreshCw } from 'lucide-react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { AdminShell } from '../../../shared/components/AdminShell';
import { authMessage } from '../../auth/hooks/authMessage';
import { useSessionViewModel } from '../../auth/hooks/useSessionViewModel';
import { loginVariants } from '../../auth/loginVariants';
import { accessRoles, type Session } from '../../auth/types/auth.types';
import { useDetalleSolicitudViewModel } from '../hooks/useDetalleSolicitudViewModel';
import '../solicitudes.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

const DETALLE_SKELETON_ITEMS = [
  'Folio',
  'Estatus',
  'Fecha y hora de entrega',
  'Plazo de vencimiento',
  'Días restantes',
  'Medio de entrega',
  'Área o dependencia receptora',
  'Asunto corto',
] as const;

function DetalleSolicitudWorkspace({
  session,
  folio,
  logout,
}: {
  session: Session;
  folio: string;
  logout: LogoutAction;
}) {
  const vm = useDetalleSolicitudViewModel(folio);

  return (
    <AdminShell
      session={session}
      title="Detalle de solicitud"
      breadcrumbParent="Solicitudes"
      breadcrumbCurrent={vm.folioTitle}
      onNavigateDashboard={vm.irADashboard}
      onNavigateSolicitudes={vm.volverAlListado}
      logout={logout}
    >
      <section className="solicitud-card" aria-labelledby="detalle-solicitud-titulo">
        {vm.loadErrorType === 'b04' && (
          <div role="alert" className="solicitud-banner solicitud-banner-error">
            <div className="solicitud-banner-content">
              <AlertCircle size={18} aria-hidden="true" />
              <span>No se pudo cargar la solicitud.</span>
            </div>
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              onClick={vm.retryLoad}
            >
              <RefreshCw size={15} aria-hidden="true" />
              Reintentar
            </button>
          </div>
        )}

        {vm.loadErrorType === 'unauthorized' && (
          <div role="alert" className="solicitud-banner solicitud-banner-error">
            <div className="solicitud-banner-content">
              <AlertCircle size={18} aria-hidden="true" />
              <span>Tu sesión expiró. Inicia sesión nuevamente para continuar.</span>
            </div>
          </div>
        )}

        {vm.loadErrorType === 'forbidden' && (
          <div role="alert" className="solicitud-banner solicitud-banner-error">
            <div className="solicitud-banner-content">
              <AlertCircle size={18} aria-hidden="true" />
              <span>No tienes permisos para consultar esta solicitud.</span>
            </div>
          </div>
        )}

        <div className="solicitud-card-header">
          <h2 id="detalle-solicitud-titulo" className="solicitud-card-title">
            Información de la solicitud
          </h2>
          <span className="solicitud-folio-chip-immutable">
            <Hash size={14} aria-hidden="true" />
            <span>{`Folio ${vm.folioTitle}`}</span>
          </span>
        </div>

        {vm.isLoading && (
          <div
            role="status"
            aria-busy="true"
            aria-label="Cargando detalle de solicitud"
            className="solicitud-grid"
          >
            {DETALLE_SKELETON_ITEMS.map((label) => (
              <div key={label} className="solicitud-field">
                <span className="solicitud-label">{label}</span>
                <div
                  data-testid="detalle-skeleton-control"
                  className="solicitud-skeleton-control"
                />
              </div>
            ))}
          </div>
        )}

        {vm.isNotFound && (
          <div className="v04-empty-state" data-testid="detalle-not-found">
            <div className="v04-empty-icon" aria-hidden="true">
              <FileSearch size={26} />
            </div>
            <h3 className="v04-empty-title">Solicitud no disponible</h3>
            <p className="v04-empty-text">
              {`La solicitud ${vm.folioTitle} no existe o fue eliminada.`}
            </p>
          </div>
        )}

        {vm.detalle && (
          <dl className="solicitud-summary-grid">
            <div className="solicitud-summary-item">
              <dt>Folio</dt>
              <dd>{vm.detalle.folio}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Estatus</dt>
              <dd>
                <span className={`v04-estatus-badge is-${vm.detalle.estatusModifier}`}>
                  {vm.detalle.estatusLabel}
                </span>
              </dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Fecha y hora de entrega</dt>
              <dd>{`${vm.detalle.fechaEntregaFormatted} ${vm.detalle.horaEntrega}`}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Plazo de vencimiento</dt>
              <dd>{vm.detalle.fechaVencimientoFormatted}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Días restantes</dt>
              <dd>
                {vm.detalle.isEntregada ? (
                  <span className="v04-dias-dash">—</span>
                ) : (
                  <span className={`v04-dias-chip is-${vm.detalle.diasModifier}`}>
                    {vm.detalle.diasLabel}
                  </span>
                )}
              </dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Medio de entrega</dt>
              <dd>{vm.detalle.medioEntregaNombre}</dd>
            </div>

            <div className="solicitud-summary-item solicitud-col-full">
              <dt>Área o dependencia receptora</dt>
              <dd>{vm.detalle.areaReceptoraNombre}</dd>
            </div>

            <div className="solicitud-summary-item solicitud-col-full">
              <dt>Asunto corto</dt>
              <dd>{vm.detalle.asunto}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Tipo de solicitud</dt>
              <dd>{vm.detalle.tipoSolicitudNombre}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Evidencia que se lleva a cabo</dt>
              <dd>{vm.detalle.tipoEvidenciaNombre}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>¿Tiene acuse?</dt>
              <dd>{vm.detalle.tieneAcuseTexto}</dd>
            </div>

            <div className="solicitud-summary-item">
              <dt>Tipo de acuse</dt>
              <dd>{vm.detalle.tipoAcuseNombre}</dd>
            </div>
          </dl>
        )}

        <div className="solicitud-actions-bar">
          <div className="solicitud-actions-buttons">
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              onClick={vm.volverAlListado}
            >
              <ArrowLeft size={16} aria-hidden="true" />
              Volver al listado
            </button>
          </div>
        </div>
      </section>
    </AdminShell>
  );
}

export function DetalleSolicitudPage() {
  const { folio } = useParams<{ folio: string }>();
  const { session, logout } = useSessionViewModel();

  if (session.isPending) {
    return <main role="status">Comprobando sesión…</main>;
  }

  if (session.isError) {
    return (
      <main className="mx-auto max-w-2xl space-y-4 px-6 py-12">
        <p role="alert">{authMessage(session.error)}</p>
        <button type="button" onClick={() => void session.refetch()}>
          Volver a comprobar la sesión
        </button>
      </main>
    );
  }

  if (!session.data) {
    return <Navigate to="/admin/login" replace />;
  }

  const isAdmin = session.data.roles.includes(accessRoles.admin);
  if (!isAdmin) {
    return (
      <main className="mx-auto max-w-2xl space-y-4 px-6 py-12">
        <h1>Acceso denegado</h1>
        <p>Tu sesión no tiene permiso para esta sección.</p>
        {session.data.roles.includes(accessRoles.consulta) && (
          <Link to={loginVariants.consulta.destination}>Ir a mi panel</Link>
        )}
        {logout.isError && <p role="alert">{authMessage(logout.error)}</p>}
        <button type="button" disabled={logout.isPending} onClick={() => logout.mutate()}>
          {logout.isPending ? 'Cerrando sesión…' : 'Cerrar sesión'}
        </button>
      </main>
    );
  }

  const normalizedFolio = folio?.trim() ?? '';
  if (!normalizedFolio) {
    return <Navigate to="/admin/solicitudes" replace />;
  }

  return (
    <DetalleSolicitudWorkspace
      key={`${session.data.userId}:${normalizedFolio}`}
      session={session.data}
      folio={normalizedFolio}
      logout={logout}
    />
  );
}
