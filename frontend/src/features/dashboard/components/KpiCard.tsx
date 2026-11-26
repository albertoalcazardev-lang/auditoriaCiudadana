import {
  AlertCircle,
  CalendarClock,
  CheckCircle2,
  Clock,
  FileText,
  type LucideIcon,
} from 'lucide-react';
import type { KpiCardId, KpiCardViewModel } from '../types/dashboard.types';

const KPI_ICONS: Record<KpiCardId, LucideIcon> = {
  total: FileText,
  'en-proceso': Clock,
  entregadas: CheckCircle2,
  vencidas: AlertCircle,
  'proximas-a-vencer': CalendarClock,
};

interface KpiCardProps {
  card: KpiCardViewModel;
}

export function KpiCard({ card }: KpiCardProps) {
  const Icon = KPI_ICONS[card.id];

  if (card.state === 'loading') {
    return (
      <article
        className="dashboard-kpi-card"
        data-testid={`kpi-card-${card.id}`}
        aria-busy="true"
        aria-label={`${card.label}: cargando`}
      >
        <div className="dashboard-kpi-top">
          <div className="dashboard-kpi-text">
            <span className="dashboard-kpi-label">{card.label}</span>
            <span className="dashboard-skeleton dashboard-skeleton-kpi-value" aria-hidden="true" />
          </div>
          <span
            className="dashboard-kpi-icon"
            style={{ backgroundColor: card.iconBg, color: card.iconColor }}
            aria-hidden="true"
          >
            <Icon size={20} />
          </span>
        </div>
        <span className="dashboard-skeleton dashboard-skeleton-kpi-sub" aria-hidden="true" />
      </article>
    );
  }

  return (
    <article
      className={`dashboard-kpi-card ${card.state === 'error' ? 'is-error' : ''}`}
      data-testid={`kpi-card-${card.id}`}
    >
      <div className="dashboard-kpi-top">
        <div className="dashboard-kpi-text">
          <span className="dashboard-kpi-label">{card.label}</span>
          <strong className="dashboard-kpi-value" data-testid={`kpi-value-${card.id}`}>
            {card.valueText}
          </strong>
        </div>
        <span
          className="dashboard-kpi-icon"
          style={{ backgroundColor: card.iconBg, color: card.iconColor }}
          aria-hidden="true"
        >
          <Icon size={20} />
        </span>
      </div>

      {card.secondaryText !== null && (
        <span
          className={`dashboard-kpi-secondary ${card.state === 'error' ? 'is-error' : ''}`}
          data-testid={`kpi-secondary-${card.id}`}
        >
          {card.secondaryText}
        </span>
      )}
    </article>
  );
}
