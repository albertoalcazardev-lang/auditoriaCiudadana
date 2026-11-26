import { RefreshCw } from 'lucide-react';
import { Link, Navigate } from 'react-router-dom';
import { AdminShell } from '../../../shared/components/AdminShell';
import { authMessage } from '../../auth/hooks/authMessage';
import { useSessionViewModel } from '../../auth/hooks/useSessionViewModel';
import { loginVariants } from '../../auth/loginVariants';
import { accessRoles, type Session } from '../../auth/types/auth.types';
import { AreaBarChart } from '../components/AreaBarChart';
import { KpiCard } from '../components/KpiCard';
import { PeriodSelect } from '../components/PeriodSelect';
import { RecentActivity } from '../components/RecentActivity';
import { StatusDonut } from '../components/StatusDonut';
import { UpcomingDeadlines } from '../components/UpcomingDeadlines';
import { useDashboardViewModel } from '../hooks/useDashboardViewModel';
import '../dashboard.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

function DashboardWorkspace({
  session,
  logout,
}: {
  session: Session;
  logout: LogoutAction;
}) {
  const vm = useDashboardViewModel();

  return (
    <AdminShell
      session={session}
      title="Dashboard"
      subtitle="Resumen general de las solicitudes"
      activeSection="dashboard"
      onNavigateDashboard={vm.stayOnDashboard}
      onNavigateSolicitudes={vm.goToSolicitudes}
      logout={logout}
    >
      <div className="dashboard-page">
        <div className="dashboard-toolbar">
          <div className="dashboard-toolbar-left">
            <span
              role="status"
              aria-live="polite"
              data-testid="dashboard-updated-status"
              className={`dashboard-updated-text ${vm.isTotalError ? 'is-error' : ''}`}
            >
              {vm.toolbarStatusText}
            </span>
            <button
              type="button"
              className="dashboard-refresh-btn"
              aria-label="Actualizar indicadores"
              title="Actualizar indicadores"
              disabled={vm.isFetching}
              onClick={vm.refresh}
            >
              <RefreshCw
                size={16}
                aria-hidden="true"
                className={vm.isFetching ? 'dashboard-spin' : undefined}
              />
            </button>
            {vm.kpisState === 'error' && (
              <button
                type="button"
                className="dashboard-retry-btn dashboard-toolbar-retry"
                data-testid="kpi-retry-btn"
                onClick={vm.refresh}
              >
                Reintentar
              </button>
            )}
          </div>

          <div className="dashboard-toolbar-right">
            <PeriodSelect
              value={vm.periodo}
              options={vm.periodoOptions}
              onChange={vm.onPeriodoChange}
            />
          </div>
        </div>

        <section
          className="dashboard-kpi-grid"
          aria-label="Indicadores principales"
          data-testid="dashboard-kpi-grid"
        >
          {vm.kpis.map((card) => (
            <KpiCard key={card.id} card={card} />
          ))}
        </section>

        <div className="dashboard-row-grid">
          <div className="dashboard-col-donut">
            <StatusDonut vm={vm.donut} onRetry={vm.refresh} />
          </div>
          <div className="dashboard-col-deadlines">
            <UpcomingDeadlines
              vm={vm.deadlines}
              onSelectFolio={vm.selectDeadlineFolio}
              onRetry={vm.refresh}
            />
          </div>
        </div>

        <div className="dashboard-row-grid">
          <div className="dashboard-col-areas">
            <AreaBarChart vm={vm.areaChart} onRetry={vm.refresh} />
          </div>
          <div className="dashboard-col-activity">
            <RecentActivity vm={vm.recentActivity} onRetry={vm.refresh} />
          </div>
        </div>
      </div>
    </AdminShell>
  );
}

export function DashboardPage() {
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

  return <DashboardWorkspace session={session.data} logout={logout} />;
}
