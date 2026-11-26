import {
  AlertCircle,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Eye,
  Pencil,
  Plus,
  RefreshCw,
  SearchX,
  Trash2,
} from 'lucide-react';
import { formatIsoToDdMmYyyy } from '../hooks/solicitudesDateUtils';
import {
  buildPaginationRange,
  formatEstatusEfectivoLabel,
  formatMedioEntregaLabel,
  getDiasChipModifier,
  getEstatusBadgeModifier,
} from '../hooks/solicitudesListadoUtils';
import type { ListadoLoadErrorType } from '../hooks/useSolicitudesListadoViewModel';
import type {
  SolicitudListadoItem,
  SolicitudPageSize,
  SolicitudSortDirection,
  SolicitudSortField,
} from '../types/solicitudes.types';

const SKELETON_ROWS = [0, 1, 2, 3, 4, 5] as const;

interface SolicitudesTablaProps {
  items: SolicitudListadoItem[];
  isLoading: boolean;
  loadErrorType: ListadoLoadErrorType;
  isEmptyNoMatches: boolean;
  isEmptyNoSolicitudes: boolean;
  showPagination: boolean;
  totalItems: number;
  totalPages: number;
  currentPage: number;
  pageSize: SolicitudPageSize;
  sortField: SolicitudSortField;
  sortDir: SolicitudSortDirection;
  isMobileViewport: boolean;
  onSort: (field: SolicitudSortField) => void;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: SolicitudPageSize) => void;
  onRetryLoad: () => void;
  onClearAllCriteria: () => void;
  onNuevaSolicitud: () => void;
  onVerSolicitud: (folio: string) => void;
  onEditarSolicitud: (folio: string) => void;
  onEliminarSolicitud: (item: SolicitudListadoItem) => void;
}

function SortIcon({
  active,
  dir,
}: {
  active: boolean;
  dir: SolicitudSortDirection;
}) {
  if (!active) {
    return <ArrowUpDown size={13} aria-hidden="true" />;
  }
  return dir === 'asc' ? (
    <ArrowUp size={13} aria-hidden="true" />
  ) : (
    <ArrowDown size={13} aria-hidden="true" />
  );
}

function renderDiasCell(item: SolicitudListadoItem) {
  if (item.estatusEfectivo === 'Entregada') {
    return <span className="v04-dias-dash">—</span>;
  }
  const dias = Math.max(0, item.diasRestantes);
  const modifier = getDiasChipModifier(dias);
  return (
    <span className={`v04-dias-chip is-${modifier}`}>
      {`${dias} ${dias === 1 ? 'día' : 'días'}`}
    </span>
  );
}

export function SolicitudesTabla({
  items,
  isLoading,
  loadErrorType,
  isEmptyNoMatches,
  isEmptyNoSolicitudes,
  showPagination,
  totalItems,
  totalPages,
  currentPage,
  pageSize,
  sortField,
  sortDir,
  isMobileViewport,
  onSort,
  onPageChange,
  onPageSizeChange,
  onRetryLoad,
  onClearAllCriteria,
  onNuevaSolicitud,
  onVerSolicitud,
  onEditarSolicitud,
  onEliminarSolicitud,
}: SolicitudesTablaProps) {
  if (loadErrorType === 'b04') {
    return (
      <div role="alert" className="solicitud-banner solicitud-banner-error">
        <div className="solicitud-banner-content">
          <AlertCircle size={18} aria-hidden="true" />
          <span>No se pudo cargar la lista de solicitudes.</span>
        </div>
        <button
          type="button"
          className="solicitud-btn solicitud-btn-secondary"
          onClick={onRetryLoad}
        >
          <RefreshCw size={15} aria-hidden="true" />
          Reintentar
        </button>
      </div>
    );
  }

  if (loadErrorType === 'unauthorized') {
    return (
      <div role="alert" className="solicitud-banner solicitud-banner-error">
        <div className="solicitud-banner-content">
          <AlertCircle size={18} aria-hidden="true" />
          <span>Tu sesión expiró. Inicia sesión nuevamente para continuar.</span>
        </div>
      </div>
    );
  }

  if (loadErrorType === 'forbidden') {
    return (
      <div role="alert" className="solicitud-banner solicitud-banner-error">
        <div className="solicitud-banner-content">
          <AlertCircle size={18} aria-hidden="true" />
          <span>No tienes permisos para consultar el listado de solicitudes.</span>
        </div>
      </div>
    );
  }

  if (isEmptyNoMatches) {
    return (
      <section className="v04-table-card" aria-label="Sin coincidencias">
        <div className="v04-empty-state" data-testid="empty-no-matches">
          <div className="v04-empty-icon" aria-hidden="true">
            <SearchX size={26} />
          </div>
          <h2 className="v04-empty-title">No se encontraron solicitudes</h2>
          <p className="v04-empty-text">
            Ninguna solicitud coincide con los criterios aplicados.
          </p>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-secondary"
            onClick={onClearAllCriteria}
          >
            Limpiar filtros
          </button>
        </div>
      </section>
    );
  }

  if (isEmptyNoSolicitudes) {
    return (
      <section className="v04-table-card" aria-label="Sin solicitudes">
        <div className="v04-empty-state" data-testid="empty-no-solicitudes">
          <div className="v04-empty-icon" aria-hidden="true">
            <ClipboardList size={26} />
          </div>
          <h2 className="v04-empty-title">Aún no hay solicitudes registradas.</h2>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-primary"
            onClick={onNuevaSolicitud}
          >
            <Plus size={16} aria-hidden="true" />
            Nueva solicitud
          </button>
        </div>
      </section>
    );
  }

  const startItem =
    totalItems === 0 ? 0 : Math.min(totalItems, (currentPage - 1) * pageSize + 1);
  const endItem =
    totalItems === 0
      ? 0
      : isLoading
        ? Math.min(totalItems, currentPage * pageSize)
        : Math.min(totalItems, startItem + Math.max(0, items.length - 1));

  const pageRange = buildPaginationRange(currentPage, totalPages);

  const getAriaSort = (field: SolicitudSortField): 'ascending' | 'descending' | 'none' => {
    if (sortField !== field) return 'none';
    return sortDir === 'asc' ? 'ascending' : 'descending';
  };

  return (
    <section className="v04-table-card" aria-label="Resultados de solicitudes">
      {isLoading && (
        <div
          role="status"
          aria-live="polite"
          data-testid="solicitudes-loading-indicator"
          className="v04-loading-sr"
        >
          Cargando solicitudes…
        </div>
      )}

      {!isMobileViewport ? (
        <div className="v04-table-scroll">
          <table className="v04-table" aria-busy={isLoading}>
            <thead>
              <tr>
                <th scope="col" className="v04-col-folio" aria-sort={getAriaSort('folio')}>
                  <button
                    type="button"
                    className={`v04-sort-btn ${sortField === 'folio' ? 'is-active' : ''}`}
                    onClick={() => onSort('folio')}
                  >
                    <span>Folio</span>
                    <SortIcon active={sortField === 'folio'} dir={sortDir} />
                  </button>
                </th>
                <th scope="col" className="v04-col-asunto">
                  Asunto
                </th>
                <th scope="col" className="v04-col-area">
                  Área receptora
                </th>
                <th scope="col" className="v04-col-medio">
                  Medio
                </th>
                <th scope="col" className="v04-col-estatus">
                  Estatus
                </th>
                <th
                  scope="col"
                  className="v04-col-entrega"
                  aria-sort={getAriaSort('entrega')}
                >
                  <button
                    type="button"
                    className={`v04-sort-btn ${sortField === 'entrega' ? 'is-active' : ''}`}
                    onClick={() => onSort('entrega')}
                  >
                    <span>Entrega</span>
                    <SortIcon active={sortField === 'entrega'} dir={sortDir} />
                  </button>
                </th>
                <th
                  scope="col"
                  className="v04-col-vencimiento"
                  aria-sort={getAriaSort('vencimiento')}
                >
                  <button
                    type="button"
                    className={`v04-sort-btn ${sortField === 'vencimiento' ? 'is-active' : ''}`}
                    onClick={() => onSort('vencimiento')}
                  >
                    <span>Vencimiento</span>
                    <SortIcon active={sortField === 'vencimiento'} dir={sortDir} />
                  </button>
                </th>
                <th scope="col" className="v04-col-dias" aria-sort={getAriaSort('dias')}>
                  <button
                    type="button"
                    className={`v04-sort-btn ${sortField === 'dias' ? 'is-active' : ''}`}
                    onClick={() => onSort('dias')}
                  >
                    <span>Días restantes</span>
                    <SortIcon active={sortField === 'dias'} dir={sortDir} />
                  </button>
                </th>
                <th scope="col" className="v04-col-acciones">
                  Acciones
                </th>
              </tr>
            </thead>

            <tbody>
              {isLoading
                ? SKELETON_ROWS.map((idx) => (
                    <tr key={idx} data-testid="solicitud-skeleton-row">
                      <td className="v04-col-folio">
                        <div className="v04-skeleton-bar" style={{ width: '85%' }} />
                      </td>
                      <td className="v04-col-asunto">
                        <div className="v04-skeleton-bar" style={{ width: '92%' }} />
                      </td>
                      <td className="v04-col-area">
                        <div className="v04-skeleton-bar" style={{ width: '80%' }} />
                      </td>
                      <td className="v04-col-medio">
                        <div className="v04-skeleton-bar" style={{ width: '70%' }} />
                      </td>
                      <td className="v04-col-estatus">
                        <div className="v04-skeleton-bar" style={{ width: '78%' }} />
                      </td>
                      <td className="v04-col-entrega">
                        <div className="v04-skeleton-bar" style={{ width: '82%' }} />
                      </td>
                      <td className="v04-col-vencimiento">
                        <div className="v04-skeleton-bar" style={{ width: '82%' }} />
                      </td>
                      <td className="v04-col-dias">
                        <div className="v04-skeleton-bar" style={{ width: '68%' }} />
                      </td>
                      <td className="v04-col-acciones">
                        <div className="v04-skeleton-bar" style={{ width: '90%' }} />
                      </td>
                    </tr>
                  ))
                : items.map((item) => {
                    const estatusMod = getEstatusBadgeModifier(item.estatusEfectivo);
                    return (
                      <tr key={item.id}>
                        <td className="v04-col-folio">
                          <span className="v04-folio-text">{item.folio}</span>
                        </td>
                        <td className="v04-col-asunto">
                          <span className="v04-clamp-2" title={item.asunto}>
                            {item.asunto}
                          </span>
                        </td>
                        <td className="v04-col-area">
                          <span className="v04-clamp-2" title={item.areaReceptora}>
                            {item.areaReceptora}
                          </span>
                        </td>
                        <td className="v04-col-medio">
                          {formatMedioEntregaLabel(item.medioEntrega)}
                        </td>
                        <td className="v04-col-estatus">
                          <span className={`v04-estatus-badge is-${estatusMod}`}>
                            {formatEstatusEfectivoLabel(item.estatusEfectivo)}
                          </span>
                        </td>
                        <td className="v04-col-entrega">
                          {formatIsoToDdMmYyyy(item.fechaEntrega)}
                        </td>
                        <td className="v04-col-vencimiento">
                          {formatIsoToDdMmYyyy(item.fechaVencimiento)}
                        </td>
                        <td className="v04-col-dias">{renderDiasCell(item)}</td>
                        <td className="v04-col-acciones">
                          <div className="v04-row-actions">
                            <button
                              type="button"
                              className="v04-action-icon-btn"
                              title={`Ver solicitud ${item.folio}`}
                              aria-label={`Ver solicitud ${item.folio}`}
                              onClick={() => onVerSolicitud(item.folio)}
                            >
                              <Eye size={16} aria-hidden="true" />
                            </button>
                            <button
                              type="button"
                              className="v04-action-icon-btn"
                              title={`Editar solicitud ${item.folio}`}
                              aria-label={`Editar solicitud ${item.folio}`}
                              onClick={() => onEditarSolicitud(item.folio)}
                            >
                              <Pencil size={16} aria-hidden="true" />
                            </button>
                            <button
                              type="button"
                              className="v04-action-icon-btn is-danger"
                              title={`Eliminar solicitud ${item.folio}`}
                              aria-label={`Eliminar solicitud ${item.folio}`}
                              onClick={() => onEliminarSolicitud(item)}
                            >
                              <Trash2 size={16} aria-hidden="true" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
            </tbody>
          </table>
        </div>
      ) : (
        <div
          className="v04-cards-list"
          aria-busy={isLoading}
          data-testid="solicitudes-mobile-cards"
        >
          {isLoading
            ? SKELETON_ROWS.map((idx) => (
                <div
                  key={idx}
                  data-testid="solicitud-skeleton-row"
                  className="v04-mobile-card"
                >
                  <div className="v04-skeleton-bar" style={{ width: '50%' }} />
                  <div className="v04-skeleton-bar" style={{ width: '90%' }} />
                  <div className="v04-skeleton-bar" style={{ width: '70%' }} />
                </div>
              ))
            : items.map((item) => {
                const estatusMod = getEstatusBadgeModifier(item.estatusEfectivo);
                return (
                  <article
                    key={item.id}
                    data-testid="solicitud-mobile-card"
                    className="v04-mobile-card"
                  >
                    <div className="v04-mobile-card-top">
                      <span className="v04-folio-text">{item.folio}</span>
                      <span className={`v04-estatus-badge is-${estatusMod}`}>
                        {formatEstatusEfectivoLabel(item.estatusEfectivo)}
                      </span>
                    </div>

                    <p className="v04-mobile-card-asunto v04-clamp-2">{item.asunto}</p>
                    <p className="v04-mobile-card-area v04-clamp-2">{item.areaReceptora}</p>

                    <div className="v04-mobile-card-meta">
                      <span>
                        Vence: <strong>{formatIsoToDdMmYyyy(item.fechaVencimiento)}</strong>
                      </span>
                      <span>Días: {renderDiasCell(item)}</span>
                    </div>

                    <div className="v04-mobile-card-footer">
                      <span>{formatMedioEntregaLabel(item.medioEntrega)}</span>
                      <div className="v04-row-actions">
                        <button
                          type="button"
                          className="v04-action-icon-btn"
                          title={`Ver solicitud ${item.folio}`}
                          aria-label={`Ver solicitud ${item.folio}`}
                          onClick={() => onVerSolicitud(item.folio)}
                        >
                          <Eye size={16} aria-hidden="true" />
                        </button>
                        <button
                          type="button"
                          className="v04-action-icon-btn"
                          title={`Editar solicitud ${item.folio}`}
                          aria-label={`Editar solicitud ${item.folio}`}
                          onClick={() => onEditarSolicitud(item.folio)}
                        >
                          <Pencil size={16} aria-hidden="true" />
                        </button>
                        <button
                          type="button"
                          className="v04-action-icon-btn is-danger"
                          title={`Eliminar solicitud ${item.folio}`}
                          aria-label={`Eliminar solicitud ${item.folio}`}
                          onClick={() => onEliminarSolicitud(item)}
                        >
                          <Trash2 size={16} aria-hidden="true" />
                        </button>
                      </div>
                    </div>
                  </article>
                );
              })}
        </div>
      )}

      {showPagination && (
        <div className="v04-pagination-bar">
          <div className="v04-pagination-meta">
            <span>{`Mostrando ${startItem} a ${endItem} de ${totalItems} solicitudes`}</span>

            <div className="v04-size-control">
              <label htmlFor="v04-page-size">Mostrar</label>
              <select
                id="v04-page-size"
                aria-label="Solicitudes por página"
                className="v04-size-select"
                value={pageSize}
                onChange={(event) =>
                  onPageSizeChange(Number(event.target.value) as SolicitudPageSize)
                }
              >
                <option value={10}>10</option>
                <option value={25}>25</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>

          <nav aria-label="Paginación de solicitudes" className="v04-page-nav">
            <button
              type="button"
              className="v04-page-btn"
              aria-label="Página anterior"
              disabled={currentPage <= 1}
              onClick={() => onPageChange(currentPage - 1)}
            >
              <ChevronLeft size={16} aria-hidden="true" />
            </button>

            {pageRange.map((entry, idx) =>
              entry === 'ellipsis' ? (
                <span key={`ellipsis-${idx}`} className="v04-page-ellipsis" aria-hidden="true">
                  …
                </span>
              ) : (
                <button
                  key={entry}
                  type="button"
                  className="v04-page-btn"
                  aria-label={`Página ${entry}`}
                  aria-current={entry === currentPage ? 'page' : undefined}
                  onClick={() => onPageChange(entry)}
                >
                  {entry}
                </button>
              ),
            )}

            <button
              type="button"
              className="v04-page-btn"
              aria-label="Página siguiente"
              disabled={currentPage >= totalPages}
              onClick={() => onPageChange(currentPage + 1)}
            >
              <ChevronRight size={16} aria-hidden="true" />
            </button>
          </nav>
        </div>
      )}
    </section>
  );
}
