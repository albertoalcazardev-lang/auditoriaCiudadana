import { Link, Navigate } from 'react-router-dom';
import { AdminShell } from '../../../shared/components/AdminShell';
import { authMessage } from '../../auth/hooks/authMessage';
import { useSessionViewModel } from '../../auth/hooks/useSessionViewModel';
import { loginVariants } from '../../auth/loginVariants';
import { accessRoles, type Session } from '../../auth/types/auth.types';
import { ConfirmacionModal } from '../components/ConfirmacionModal';
import { NuevaSolicitudForm } from '../components/NuevaSolicitudForm';
import { SolicitudConfirmacionView } from '../components/SolicitudConfirmacionView';
import { useNuevaSolicitudViewModel } from '../hooks/useNuevaSolicitudViewModel';
import '../solicitudes.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

function NuevaSolicitudWorkspace({
  session,
  logout,
}: {
  session: Session;
  logout: LogoutAction;
}) {
  const vm = useNuevaSolicitudViewModel(session.userId);

  return (
    <AdminShell
      session={session}
      title="Nueva solicitud"
      breadcrumbParent="Solicitudes"
      breadcrumbCurrent="Nueva solicitud"
      onNavigateDashboard={vm.requestExitToDashboard}
      logout={logout}
    >
      {vm.submitStatus === 'succeeded' && vm.confirmacion ? (
        <SolicitudConfirmacionView
          confirmacion={vm.confirmacion}
          onRegistrarOtra={vm.resetFormulario}
          onVolverPanel={vm.confirmExitToDashboard}
        />
      ) : (
        <NuevaSolicitudForm vm={vm} />
      )}

      <ConfirmacionModal
        open={vm.modalState === 'confirm-register'}
        title="¿Registrar solicitud?"
        message="Se creará una nueva solicitud con los datos capturados."
        detail={
          vm.values.generarFolioAutomatico ? (
            <span>
              <strong>Folio:</strong> Se asignará automáticamente al guardar
            </span>
          ) : (
            <span>
              <strong>Folio:</strong> {vm.values.folio.trim()}
            </span>
          )
        }
        busy={vm.submitStatus === 'submitting'}
        onClose={vm.cancelConfirmRegister}
        actions={[
          {
            label: 'Cancelar',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.cancelConfirmRegister,
          },
          {
            label: vm.submitStatus === 'submitting' ? 'Registrando…' : 'Registrar',
            variant: 'primary',
            onClick: vm.confirmRegister,
          },
        ]}
      />

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
            onClick: vm.confirmExitToDashboard,
          },
        ]}
      />

      <ConfirmacionModal
        open={vm.modalState === 'confirm-uncertain-exit'}
        title="Envío con resultado incierto"
        message="El resultado de la solicitud anterior es incierto y podría haberse registrado. Puedes reintentar de forma segura para recuperar el resultado, permanecer aquí o salir al panel."
        busy={vm.submitStatus === 'submitting'}
        onClose={vm.closeDiscardModal}
        actions={[
          {
            label: 'Seguir aquí',
            variant: 'secondary',
            initialFocus: true,
            onClick: vm.closeDiscardModal,
          },
          {
            label: 'Reintentar',
            variant: 'primary',
            onClick: vm.retryUncertainSubmission,
          },
          {
            label: 'Salir al panel',
            variant: 'danger',
            onClick: vm.confirmExitToDashboard,
          },
        ]}
      />
    </AdminShell>
  );
}

export function NuevaSolicitudPage() {
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

  return <NuevaSolicitudWorkspace session={session.data} logout={logout} />;
}
