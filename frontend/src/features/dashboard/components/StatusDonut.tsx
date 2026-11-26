import { useState } from 'react';
import type { DonutSegmentViewModel, StatusDonutViewModel } from '../types/dashboard.types';

interface StatusDonutProps {
  vm: StatusDonutViewModel;
  onRetry: () => void;
}

const SVG_SIZE = 180;
const STROKE_WIDTH = 28;
const RADIUS = (SVG_SIZE - STROKE_WIDTH) / 2; // 76
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

export function StatusDonut({ vm, onRetry }: StatusDonutProps) {
  const [activeId, setActiveId] = useState<DonutSegmentViewModel['id'] | null>(null);
  const activeSegment = vm.segments.find((seg) => seg.id === activeId) ?? null;

  let cumulativeLength = 0;
  const renderedArcs =
    vm.state === 'ready' && vm.total > 0
      ? vm.segments
          .filter((seg) => seg.value > 0)
          .map((seg) => {
            const length = (seg.value / vm.total) * CIRCUMFERENCE;
            const offset = -cumulativeLength;
            cumulativeLength += length;
            return {
              ...seg,
              dashArray: `${length} ${Math.max(0, CIRCUMFERENCE - length)}`,
              dashOffset: offset,
            };
          })
      : [];

  return (
    <section
      className="dashboard-card dashboard-donut-card"
      data-testid="widget-status-donut"
      aria-labelledby="dashboard-donut-heading"
    >
      <header className="dashboard-card-header">
        <h2 id="dashboard-donut-heading" className="dashboard-card-title">
          Solicitudes por estatus
        </h2>
      </header>

      {vm.state === 'loading' && (
        <div className="dashboard-donut-loading" aria-busy="true">
          <div className="dashboard-skeleton dashboard-skeleton-donut" aria-hidden="true" />
          <div className="dashboard-donut-legend-skeleton" aria-hidden="true">
            <span className="dashboard-skeleton dashboard-skeleton-line" />
            <span className="dashboard-skeleton dashboard-skeleton-line" />
            <span className="dashboard-skeleton dashboard-skeleton-line" />
          </div>
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
        <div className="dashboard-widget-feedback" data-testid="donut-empty-state">
          <p className="dashboard-feedback-title">Sin solicitudes en este periodo</p>
          <span className="dashboard-feedback-sub">Sin datos</span>
        </div>
      )}

      {vm.state === 'ready' && (
        <div className="dashboard-donut-body">
          <div className="dashboard-donut-figure-wrap">
            <div className="dashboard-donut-svg-container">
              <svg
                width={SVG_SIZE}
                height={SVG_SIZE}
                viewBox={`0 0 ${SVG_SIZE} ${SVG_SIZE}`}
                className="dashboard-donut-svg"
                role="group"
                aria-label={`Distribución de ${vm.total} solicitudes por estatus`}
                data-testid="donut-svg"
              >
                <g transform={`rotate(-90 ${SVG_SIZE / 2} ${SVG_SIZE / 2})`}>
                  {renderedArcs.map((arc) => {
                    const isActive = activeId === arc.id;
                    return (
                      <circle
                        key={arc.id}
                        cx={SVG_SIZE / 2}
                        cy={SVG_SIZE / 2}
                        r={RADIUS}
                        fill="transparent"
                        stroke={arc.color}
                        strokeWidth={isActive ? STROKE_WIDTH + 3 : STROKE_WIDTH}
                        strokeDasharray={arc.dashArray}
                        strokeDashoffset={arc.dashOffset}
                        tabIndex={0}
                        aria-label={`${arc.label}: ${arc.value} (${arc.percentageText})`}
                        data-testid={`donut-segment-${arc.id}`}
                        className="dashboard-donut-arc"
                        onMouseEnter={() => setActiveId(arc.id)}
                        onMouseLeave={() => setActiveId(null)}
                        onFocus={() => setActiveId(arc.id)}
                        onBlur={() => setActiveId(null)}
                      />
                    );
                  })}
                </g>
              </svg>

              <div className="dashboard-donut-center" aria-hidden="true">
                <strong className="dashboard-donut-total-value" data-testid="donut-center-total">
                  {vm.total}
                </strong>
                <span className="dashboard-donut-total-label">Total</span>
              </div>
            </div>

            {activeSegment && (
              <div
                role="tooltip"
                data-testid="donut-tooltip"
                className="dashboard-chart-tooltip dashboard-donut-tooltip"
              >
                <span
                  className="dashboard-legend-dot"
                  style={{ backgroundColor: activeSegment.color }}
                  aria-hidden="true"
                />
                <span>
                  <strong>{activeSegment.label}:</strong> {activeSegment.value} (
                  {activeSegment.percentageText})
                </span>
              </div>
            )}
          </div>

          <ul className="dashboard-donut-legend" aria-label="Leyenda de estatus">
            {vm.segments.map((seg) => (
              <li
                key={seg.id}
                className={`dashboard-donut-legend-item ${activeId === seg.id ? 'is-active' : ''}`}
                data-testid={`donut-legend-${seg.id}`}
                onMouseEnter={() => setActiveId(seg.id)}
                onMouseLeave={() => setActiveId(null)}
              >
                <span className="dashboard-donut-legend-left">
                  <span
                    className="dashboard-legend-dot"
                    style={{ backgroundColor: seg.color }}
                    aria-hidden="true"
                  />
                  <span className="dashboard-donut-legend-label">{seg.label}</span>
                </span>
                <span className="dashboard-donut-legend-right">
                  <strong className="dashboard-donut-legend-count">{seg.value}</strong>
                  <span className="dashboard-donut-legend-pct">({seg.percentageText})</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
