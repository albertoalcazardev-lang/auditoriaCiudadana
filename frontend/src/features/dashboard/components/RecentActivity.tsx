import { FilePlus2, FileText } from 'lucide-react';
import type { RecentActivityViewModel } from '../types/dashboard.types';

interface RecentActivityProps {
  vm: RecentActivityViewModel;
  onRetry: () => void;
}

export function RecentActivity({ vm, onRetry }: RecentActivityProps) {
  return (
    <section
      className="dashboard-card dashboard-activity-card"
      data-testid="widget-recent-activity"
      aria-labelledby="dashboard-activity-heading"
    >
      <header className="dashboard-card-header">
        <h2 id="dashboard-activity-heading" className="dashboard-card-title">
          Actividad reciente
        </h2>
      </header>

      {vm.state === 'loading' && (
        <div className="dashboard-activity-loading" aria-busy="true">
          <span className="dashboard-skeleton dashboard-skeleton-activity" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-activity" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-activity" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-activity" aria-hidden="true" />
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
        <div className="dashboard-widget-feedback" data-testid="activity-empty-state">
          <p className="dashboard-feedback-title">Sin actividad reciente</p>
        </div>
      )}

      {vm.state === 'ready' && (
        <ul className="dashboard-activity-list" data-testid="activity-list">
          {vm.items.map((item) => {
            const isNueva = item.tipo.toLowerCase().includes('nueva');
            const Icon = isNueva ? FilePlus2 : FileText;
            return (
              <li
                key={item.id}
                className="dashboard-activity-item"
                data-testid={`activity-item-${item.folio}`}
              >
                <span className="dashboard-activity-icon" aria-hidden="true">
                  <Icon size={16} />
                </span>
                <div className="dashboard-activity-content">
                  <p className="dashboard-activity-desc">{item.descripcion}</p>
                  <div className="dashboard-activity-meta">
                    <span className="dashboard-activity-folio">{item.folio}</span>
                    <span aria-hidden="true">·</span>
                    <span className="dashboard-activity-time">{item.tiempoRelativo}</span>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
