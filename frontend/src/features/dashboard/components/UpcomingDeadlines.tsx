import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { UpcomingDeadlinesViewModel } from '../types/dashboard.types';

interface UpcomingDeadlinesProps {
  vm: UpcomingDeadlinesViewModel;
  onSelectFolio: (folio: string) => void;
  onRetry: () => void;
}

export function UpcomingDeadlines({ vm, onSelectFolio, onRetry }: UpcomingDeadlinesProps) {
  return (
    <section
      className="dashboard-card dashboard-deadlines-card"
      data-testid="widget-upcoming-deadlines"
      aria-labelledby="dashboard-deadlines-heading"
    >
      <header className="dashboard-card-header">
        <h2 id="dashboard-deadlines-heading" className="dashboard-card-title">
          Próximos vencimientos
        </h2>
        <Link
          to={vm.verTodasHref}
          className="dashboard-ver-todas-link"
          data-testid="deadlines-ver-todas"
        >
          <span>Ver todas</span>
          <ArrowRight size={15} aria-hidden="true" />
        </Link>
      </header>

      {vm.state === 'loading' && (
        <div className="dashboard-deadlines-loading" aria-busy="true">
          <span className="dashboard-skeleton dashboard-skeleton-row" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-row" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-row" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-row" aria-hidden="true" />
        </div>
      )}

      {vm.state === 'error' && (
        <div className="dashboard-widget-feedback" role="status">
          <p className="dashboard-feedback-title">Información no disponible</p>
          <button type="button" className="dashboard-retry-btn" onClick={onRetry}>
            Reintentar
          </button>
        </div>
      )}

      {vm.state === 'empty' && (
        <div className="dashboard-widget-feedback" data-testid="deadlines-empty-state">
          <p className="dashboard-feedback-title">Sin próximos vencimientos en este periodo</p>
          <span className="dashboard-feedback-sub">Sin datos</span>
        </div>
      )}

      {vm.state === 'ready' && (
        <div className="dashboard-deadlines-table-wrap">
          <table className="dashboard-deadlines-table" data-testid="deadlines-table">
            <thead>
              <tr>
                <th scope="col" className="dashboard-col-folio">
                  Folio
                </th>
                <th scope="col" className="dashboard-col-asunto">
                  Asunto
                </th>
                <th scope="col" className="dashboard-col-vencimiento">
                  Vencimiento
                </th>
                <th scope="col" className="dashboard-col-dias">
                  Días restantes
                </th>
              </tr>
            </thead>
            <tbody>
              {vm.rows.map((row) => (
                <tr
                  key={row.folio}
                  className="dashboard-deadlines-row"
                  data-testid={`deadline-row-${row.folio}`}
                  onClick={() => onSelectFolio(row.folio)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && event.target === event.currentTarget) {
                      event.preventDefault();
                      onSelectFolio(row.folio);
                    }
                  }}
                >
                  <td className="dashboard-cell-folio" data-label="Folio">
                    <Link
                      to={row.detalleHref}
                      className="dashboard-folio-link"
                      onClick={(event) => event.stopPropagation()}
                    >
                      {row.folio}
                    </Link>
                  </td>
                  <td className="dashboard-cell-asunto" data-label="Asunto">
                    <span className="dashboard-asunto-text" title={row.asunto}>
                      {row.asunto}
                    </span>
                  </td>
                  <td className="dashboard-cell-vencimiento" data-label="Vencimiento">
                    <span>{row.fechaVencimientoTexto}</span>
                  </td>
                  <td className="dashboard-cell-dias" data-label="Días restantes">
                    <span
                      className={`dashboard-days-chip dashboard-days-chip-${row.urgencia}`}
                      data-testid={`deadline-chip-${row.folio}`}
                    >
                      {row.diasRestantesTexto}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
