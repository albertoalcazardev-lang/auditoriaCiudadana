import { CheckCircle2, Info, X } from 'lucide-react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { AdminShell } from '../../../shared/components/AdminShell';
import { authMessage } from '../../auth/hooks/authMessage';
import { useSessionViewModel } from '../../auth/hooks/useSessionViewModel';
import { loginVariants } from '../../auth/loginVariants';
import { accessRoles, type Session } from '../../auth/types/auth.types';
import { ConfirmacionModal } from '../components/ConfirmacionModal';
import { EditarSolicitudForm } from '../components/EditarSolicitudForm';
import { SolicitudConfirmacionView } from '../components/SolicitudConfirmacionView';
import { useEditarSolicitudViewModel } from '../hooks/useEditarSolicitudViewModel';
import '../solicitudes.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

function EditarSolicitudWorkspace({
  session,
  folio,
  logout,
}: {
  session: Session;
  folio: string;
  logout: LogoutAction;
}) {
  const vm = useEditarSolicitudViewModel(folio);

  return (
    <AdminShell
      session={session}
      title="Editar solicitud"
      breadcrumbParent="Solicitudes"
      breadcrumbCurrent="Editar solicitud"
      onNavigateDashboard={vm.requestExitToDashboard}
      onNavigateSolicitudes={vm.requestExitToSolicitudes}
      logout={logout}
    >
      {vm.toast && (
        <div
          role="status"
          aria-live="polite"
          data-testid="solicitud-toast"
          className={`solicitud-toast solicitud-toast-${vm.toast.tone}`}
        >
          <div className="solicitud-toast-content">
            {vm.toast.tone === 'success' ? (
              <CheckCircle2 size={18} aria-hidden="true" />
            ) : (
              <Info size={18} aria-hidden="true" />
            )}
            <span>{vm.toast.text}</span>
          </div>
          <button
            type="button"
            className="solicitud-toast-close"
            aria-label="Cerrar notificación"
            onClick={vm.dismissToast}
          >
            <X size={15} aria-hidden="true" />
          </button>
        </div>
      )}

      {vm.submitStatus === 'succeeded' && vm.confirmacion ? (
        <SolicitudConfirmacionView
          mode="updated"
          confirmacion={vm.confirmacion}
          onSeguirEditando={vm.continueEditing}
          onVolverPanel={vm.confirmExitToDashboard}
        />
      ) : (
        <EditarSolicitudForm vm={vm} />
      )}

      {/* Modal M-02: Confirmar guardado */}
      <ConfirmacionModal
        open={vm.modalState === 'confirm-save'}
        title="¿Guardar cambios?"
        message={`Se actualizará la solicitud ${vm.folioReal} con los datos modificados.`}
        busy={vm.submitStatus === 'submitting'}
        onClose={vm.cancelConfirmSave}
        actions={[
          {
            label: 'Cancelar',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.cancelConfirmSave,
          },
          {
            label: vm.submitStatus === 'submitting' ? 'Guardando cambios…' : 'Guardar cambios',
            variant: 'primary',
            onClick: vm.confirmSave,
          },
        ]}
      />

      {/* Modal M-03: Descartar cambios */}
      <ConfirmacionModal
        open={vm.modalState === 'confirm-discard'}
        title="¿Descartar cambios?"
        message="Los datos capturados no se guardarán."
        onClose={vm.closeDiscardModal}
        actions={[
          {
            label: 'Seguir editando',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.closeDiscardModal,
          },
          {
            label: 'Descartar',
            variant: 'danger',
            onClick: vm.confirmDiscardExit,
          },
        ]}
      />

      {/* Modal M-03 con operación incierta previa */}
      <ConfirmacionModal
        open={vm.modalState === 'confirm-uncertain-exit'}
        title="¿Descartar cambios?"
        message="El resultado del guardado anterior es incierto y los cambios podrían haberse aplicado en el servidor. Si descartas ahora sin verificar ni reintentar, perderás la captura en pantalla."
        busy={vm.submitStatus === 'submitting'}
        onClose={vm.closeDiscardModal}
        actions={[
          {
            label: 'Seguir editando',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.closeDiscardModal,
          },
          {
            label: 'Reintentar',
            variant: 'primary',
            onClick: vm.retryUncertainUpdate,
          },
          {
            label: 'Descartar',
            variant: 'danger',
            onClick: vm.confirmDiscardExit,
          },
        ]}
      />

      {/* Modal M-05: Conflicto de concurrencia */}
      <ConfirmacionModal
        open={vm.modalState === 'conflict-concurrency'}
        title="La solicitud cambió"
        message="Otro proceso modificó esta solicitud. Recarga para ver los datos vigentes; tus cambios no se guardaron."
        busy={vm.isReloadingDatos}
        onClose={vm.closeConflictModal}
        actions={[
          {
            label: 'Cerrar',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.closeConflictModal,
          },
          {
            label: vm.isReloadingDatos ? 'Recargando…' : 'Recargar datos',
            variant: 'primary',
            onClick: () => void vm.reloadAfterConflict(),
          },
        ]}
      />

      {/* Modal M-06: Solicitud no disponible (404 / 410) */}
      <ConfirmacionModal
        open={vm.isNotFoundModalOpen}
        title="Solicitud no disponible"
        message={`La solicitud ${vm.folioReal} ya no existe.`}
        onClose={() => {}}
        actions={[
          {
            label: 'Volver a la lista',
            variant: 'primary',
            initialFocus: true,
            onClick: vm.confirmExitToDashboard,
          },
        ]}
      />
    </AdminShell>
  );
}

export function EditarSolicitudPage() {
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
    return <Navigate to="/admin/dashboard" replace />;
  }

  return (
    <EditarSolicitudWorkspace
      key={`${session.data.userId}:${normalizedFolio}`}
      session={session.data}
      folio={normalizedFolio}
      logout={logout}
    />
  );
}
