import { useRef } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Info,
  Plus,
  RefreshCw,
  Search,
  SlidersHorizontal,
  X,
} from 'lucide-react';
import { Link, Navigate } from 'react-router-dom';
import { AdminShell } from '../../../shared/components/AdminShell';
import { authMessage } from '../../auth/hooks/authMessage';
import { useSessionViewModel } from '../../auth/hooks/useSessionViewModel';
import { loginVariants } from '../../auth/loginVariants';
import { accessRoles, type Session } from '../../auth/types/auth.types';
import { EliminarSolicitudModal } from '../components/EliminarSolicitudModal';
import { SolicitudesFiltrosPopover } from '../components/SolicitudesFiltrosPopover';
import { SolicitudesTabla } from '../components/SolicitudesTabla';
import { useSolicitudesListadoViewModel } from '../hooks/useSolicitudesListadoViewModel';
import '../solicitudes.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

function SolicitudesListadoWorkspace({
  session,
  logout,
}: {
  session: Session;
  logout: LogoutAction;
}) {
  const vm = useSolicitudesListadoViewModel();
  const filterAnchorRef = useRef<HTMLDivElement | null>(null);
  const filterTriggerRef = useRef<HTMLButtonElement | null>(null);

  return (
    <AdminShell
      session={session}
      title="Solicitudes"
      breadcrumbParent="Solicitudes"
      breadcrumbCurrent="Listado"
      onNavigateDashboard={vm.goToDashboard}
      onNavigateSolicitudes={vm.goToSolicitudes}
      logout={logout}
    >
      <div className="v04-workspace">
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

        {vm.deleteFeedback && (
          <div
            role="alert"
            data-testid="delete-feedback-banner"
            className={`solicitud-banner solicitud-banner-${vm.deleteFeedback.tone}`}
          >
            <div className="solicitud-banner-content">
              <AlertCircle size={18} aria-hidden="true" />
              <span>{vm.deleteFeedback.message}</span>
            </div>

            <div className="solicitud-BannerActions flex items-center gap-2">
              {vm.deleteFeedback.canRefreshList && (
                <button
                  type="button"
                  className="solicitud-btn solicitud-btn-secondary"
                  onClick={vm.refreshListado}
                >
                  <RefreshCw size={15} aria-hidden="true" />
                  Actualizar listado
                </button>
              )}
              <button
                type="button"
                className="solicitud-toast-close"
                aria-label="Cerrar aviso"
                onClick={vm.dismissDeleteFeedback}
              >
                <X size={15} aria-hidden="true" />
              </button>
            </div>
          </div>
        )}

        <div className="v04-toolbar">
          <div className="v04-search-box">
            <Search size={16} className="v04-search-icon" aria-hidden="true" />
            <input
              ref={vm.searchInputRef}
              type="text"
              className="v04-search-input"
              placeholder="Buscar por folio o fecha (dd/mm/aaaa)"
              aria-label="Buscar por folio o fecha (dd/mm/aaaa)"
              value={vm.searchInput}
              onChange={(event) => vm.handleSearchInputChange(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  vm.handleSearchEnter();
                }
              }}
            />
            {vm.searchInput.length > 0 && (
              <button
                type="button"
                className="v04-search-clear"
                aria-label="Limpiar búsqueda"
                onClick={vm.handleSearchClear}
              >
                <X size={15} aria-hidden="true" />
              </button>
            )}
          </div>

          <div className="v04-toolbar-actions">
            <div ref={filterAnchorRef} className="v04-filter-anchor">
              <button
                ref={filterTriggerRef}
                type="button"
                className="v04-filter-btn"
                aria-expanded={vm.isFiltersOpen}
                aria-controls="v04-filters-popover"
                onClick={vm.toggleFiltersPopover}
              >
                <SlidersHorizontal size={16} aria-hidden="true" />
                <span>Filtros</span>
                {vm.activeFilterCount > 0 && (
                  <span
                    className="v04-filter-badge"
                    data-testid="active-filters-badge"
                  >
                    {vm.activeFilterCount}
                  </span>
                )}
              </button>

              <SolicitudesFiltrosPopover
                open={vm.isFiltersOpen}
                anchorRef={filterAnchorRef}
                triggerRef={filterTriggerRef}
                draft={vm.filterDraft}
                errors={vm.filterErrors}
                onUpdateField={vm.updateFilterDraftField}
                onClear={vm.clearPopoverFilters}
                onApply={vm.applyPopoverFilters}
                onClose={vm.closeFiltersPopover}
              />
            </div>

            {!vm.isEmptyNoSolicitudes && (
              <button
                type="button"
                className="solicitud-btn solicitud-btn-primary v04-new-btn"
                aria-label="Nueva solicitud"
                onClick={vm.goToNuevaSolicitud}
              >
                <Plus size={18} aria-hidden="true" />
                <span className="v04-new-btn-label">Nueva solicitud</span>
              </button>
            )}
          </div>
        </div>

        {vm.friendlyCriteriaHint && (
          <p className="v04-search-hint" data-testid="friendly-criteria-hint">
            <Info size={16} aria-hidden="true" />
            <span>{vm.friendlyCriteriaHint}</span>
          </p>
        )}

        {vm.activeChips.length > 0 && (
          <div className="v04-chips-bar" aria-label="Criterios activos">
            {vm.activeChips.map((chip) => (
              <span
                key={chip.key}
                className="v04-chip"
                data-testid={`filter-chip-${chip.key}`}
              >
                <span>{chip.label}</span>
                <button
                  type="button"
                  className="v04-chip-remove"
                  aria-label={`Quitar filtro ${chip.label}`}
                  onClick={() => vm.removeFilterChip(chip.key)}
                >
                  <X size={13} aria-hidden="true" />
                </button>
              </span>
            ))}

            {!vm.isEmptyNoMatches && (
              <button
                type="button"
                className="v04-chips-clear-btn"
                onClick={vm.clearActiveFilters}
              >
                Limpiar filtros
              </button>
            )}
          </div>
        )}

        <SolicitudesTabla
          items={vm.items}
          isLoading={vm.isLoading}
          loadErrorType={vm.loadErrorType}
          isEmptyNoMatches={vm.isEmptyNoMatches}
          isEmptyNoSolicitudes={vm.isEmptyNoSolicitudes}
          showPagination={vm.showPagination}
          totalItems={vm.totalItems}
          totalPages={vm.totalPages}
          currentPage={vm.currentPage}
          pageSize={vm.pageSize}
          sortField={vm.urlState.sort}
          sortDir={vm.urlState.dir}
          isMobileViewport={vm.isMobileViewport}
          onSort={vm.handleSort}
          onPageChange={vm.handlePageChange}
          onPageSizeChange={vm.handlePageSizeChange}
          onRetryLoad={vm.refreshListado}
          onClearAllCriteria={vm.clearAllCriteria}
          onNuevaSolicitud={vm.goToNuevaSolicitud}
          onVerSolicitud={vm.goToVerSolicitud}
          onEditarSolicitud={vm.goToEditarSolicitud}
          onEliminarSolicitud={vm.openDeleteModal}
        />

        <EliminarSolicitudModal
          open={vm.deleteTarget !== null}
          folio={vm.deleteTarget?.folio ?? ''}
          busy={vm.isDeleting}
          onCancel={vm.closeDeleteModal}
          onConfirm={() => void vm.confirmDelete()}
        />
      </div>
    </AdminShell>
  );
}

export function SolicitudesListadoPage() {
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

  return (
    <SolicitudesListadoWorkspace
      key={session.data.userId}
      session={session.data}
      logout={logout}
    />
  );
}
