import { useState } from 'react';
import type { AreaBarChartViewModel } from '../types/dashboard.types';

interface AreaBarChartProps {
  vm: AreaBarChartViewModel;
  onRetry: () => void;
}

export function AreaBarChart({ vm, onRetry }: AreaBarChartProps) {
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const activeBar =
    activeIndex !== null && activeIndex >= 0 && activeIndex < vm.bars.length
      ? vm.bars[activeIndex]
      : null;

  return (
    <section
      className="dashboard-card dashboard-area-card"
      data-testid="widget-area-chart"
      aria-labelledby="dashboard-area-heading"
    >
      <header className="dashboard-card-header dashboard-area-header">
        <h2 id="dashboard-area-heading" className="dashboard-card-title">
          Solicitudes por área receptora
        </h2>
        {activeBar && vm.state === 'ready' && (
          <div
            role="tooltip"
            data-testid="area-chart-tooltip"
            className="dashboard-chart-tooltip dashboard-area-tooltip"
          >
            <strong>{activeBar.area}:</strong> <span>{activeBar.cantidadTexto}</span>
          </div>
        )}
      </header>

      {vm.state === 'loading' && (
        <div className="dashboard-area-loading" aria-busy="true">
          <span className="dashboard-skeleton dashboard-skeleton-bar h-3/5" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-bar h-4/5" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-bar h-2/5" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-bar h-3/4" aria-hidden="true" />
          <span className="dashboard-skeleton dashboard-skeleton-bar h-1/2" aria-hidden="true" />
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
        <div className="dashboard-widget-feedback" data-testid="area-empty-state">
          <p className="dashboard-feedback-title">
            Sin solicitudes registradas por área en este periodo
          </p>
          <span className="dashboard-feedback-sub">Sin datos</span>
        </div>
      )}

      {vm.state === 'ready' && (
        <div className="dashboard-area-scroll" data-testid="area-chart-scroll">
          <div
            className="dashboard-area-chart-inner"
            style={{ minWidth: `${Math.max(360, vm.bars.length * 76)}px` }}
          >
            <div className="dashboard-area-plot-wrap">
              <div className="dashboard-area-yaxis" aria-hidden="true">
                {[...vm.guideTicks].reverse().map((tick) => (
                  <span key={tick} className="dashboard-area-ytick">
                    {tick}
                  </span>
                ))}
                <span className="dashboard-area-ytick">0</span>
              </div>

              <div className="dashboard-area-plot">
                <div className="dashboard-area-guides" aria-hidden="true">
                  {vm.guideTicks.map((tick, idx) => (
                    <div
                      key={tick}
                      className="dashboard-area-guide-line"
                      data-testid="area-guide-line"
                      style={{ bottom: `${((idx + 1) / 4) * 100}%` }}
                    />
                  ))}
                </div>

                <div className="dashboard-area-bars-row" role="list" aria-label="Solicitudes por área">
                  {vm.bars.map((bar, index) => {
                    const heightPct = Math.max(4, (bar.cantidad / vm.scaleMax) * 100);
                    const isActive = activeIndex === index;
                    return (
                      <div
                        key={bar.area}
                        role="listitem"
                        className="dashboard-area-bar-col"
                        data-testid={`area-bar-col-${index}`}
                      >
                        <div className="dashboard-area-bar-track">
                          <button
                            type="button"
                            className={`dashboard-area-bar ${isActive ? 'is-active' : ''}`}
                            style={{ height: `${heightPct}%` }}
                            aria-label={`${bar.area}: ${bar.cantidad}`}
                            data-testid={`area-bar-${index}`}
                            data-area={bar.area}
                            data-count={bar.cantidad}
                            onMouseEnter={() => setActiveIndex(index)}
                            onMouseLeave={() => setActiveIndex(null)}
                            onFocus={() => setActiveIndex(index)}
                            onBlur={() => setActiveIndex(null)}
                          />
                        </div>
                        <span
                          className="dashboard-area-bar-label"
                          title={bar.area}
                          data-testid={`area-label-${index}`}
                        >
                          {bar.area}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
