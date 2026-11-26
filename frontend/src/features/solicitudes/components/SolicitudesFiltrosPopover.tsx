import { useEffect, type RefObject } from 'react';
import type {
  SolicitudDiasFilter,
  SolicitudesFilterDraft,
  SolicitudesFilterErrors,
} from '../types/solicitudes.types';

interface SolicitudesFiltrosPopoverProps {
  open: boolean;
  anchorRef: RefObject<HTMLDivElement | null>;
  triggerRef: RefObject<HTMLButtonElement | null>;
  draft: SolicitudesFilterDraft;
  errors: SolicitudesFilterErrors;
  onUpdateField: <K extends keyof SolicitudesFilterDraft>(
    field: K,
    value: SolicitudesFilterDraft[K],
  ) => void;
  onClear: () => void;
  onApply: () => void;
  onClose: () => void;
}

export function SolicitudesFiltrosPopover({
  open,
  anchorRef,
  triggerRef,
  draft,
  errors,
  onUpdateField,
  onClear,
  onApply,
  onClose,
}: SolicitudesFiltrosPopoverProps) {
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onClose();
        requestAnimationFrame(() => {
          triggerRef.current?.focus();
        });
      }
    };

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (target && anchorRef.current && !anchorRef.current.contains(target)) {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    document.addEventListener('mousedown', handlePointerDown);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.removeEventListener('mousedown', handlePointerDown);
    };
  }, [anchorRef, onClose, open, triggerRef]);

  if (!open) return null;

  return (
    <div
      id="v04-filters-popover"
      role="dialog"
      aria-label="Filtros de solicitudes"
      className="v04-popover"
    >
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          onApply();
        }}
      >
        <div className="v04-popover-section">
          <p className="v04-popover-heading">Fecha de entrega</p>
          <div className="v04-popover-row">
            <div>
              <label htmlFor="filtro-entrega-desde" className="v04-popover-sublabel">
                Entrega desde
              </label>
              <input
                id="filtro-entrega-desde"
                type="text"
                inputMode="numeric"
                placeholder="dd/mm/aaaa"
                className="solicitud-input"
                value={draft.entDesde}
                aria-invalid={Boolean(errors.entDesde || errors.entRange)}
                onChange={(event) => onUpdateField('entDesde', event.target.value)}
              />
            </div>

            <div>
              <label htmlFor="filtro-entrega-hasta" className="v04-popover-sublabel">
                Entrega hasta
              </label>
              <input
                id="filtro-entrega-hasta"
                type="text"
                inputMode="numeric"
                placeholder="dd/mm/aaaa"
                className="solicitud-input"
                value={draft.entHasta}
                aria-invalid={Boolean(errors.entHasta || errors.entRange)}
                onChange={(event) => onUpdateField('entHasta', event.target.value)}
              />
            </div>
          </div>
          {errors.entDesde && <p className="solicitud-field-error">{errors.entDesde}</p>}
          {errors.entHasta && <p className="solicitud-field-error">{errors.entHasta}</p>}
          {errors.entRange && <p className="solicitud-field-error">{errors.entRange}</p>}
        </div>

        <div className="v04-popover-section">
          <p className="v04-popover-heading">Fecha de vencimiento</p>
          <div className="v04-popover-row">
            <div>
              <label htmlFor="filtro-vencimiento-desde" className="v04-popover-sublabel">
                Vencimiento desde
              </label>
              <input
                id="filtro-vencimiento-desde"
                type="text"
                inputMode="numeric"
                placeholder="dd/mm/aaaa"
                className="solicitud-input"
                value={draft.venDesde}
                aria-invalid={Boolean(errors.venDesde || errors.venRange)}
                onChange={(event) => onUpdateField('venDesde', event.target.value)}
              />
            </div>

            <div>
              <label htmlFor="filtro-vencimiento-hasta" className="v04-popover-sublabel">
                Vencimiento hasta
              </label>
              <input
                id="filtro-vencimiento-hasta"
                type="text"
                inputMode="numeric"
                placeholder="dd/mm/aaaa"
                className="solicitud-input"
                value={draft.venHasta}
                aria-invalid={Boolean(errors.venHasta || errors.venRange)}
                onChange={(event) => onUpdateField('venHasta', event.target.value)}
              />
            </div>
          </div>
          {errors.venDesde && <p className="solicitud-field-error">{errors.venDesde}</p>}
          {errors.venHasta && <p className="solicitud-field-error">{errors.venHasta}</p>}
          {errors.venRange && <p className="solicitud-field-error">{errors.venRange}</p>}
        </div>

        <div className="v04-popover-section">
          <label htmlFor="filtro-dias-restantes" className="v04-popover-heading">
            Días restantes
          </label>
          <select
            id="filtro-dias-restantes"
            className="solicitud-select"
            value={draft.dias}
            onChange={(event) =>
              onUpdateField('dias', event.target.value as SolicitudDiasFilter)
            }
          >
            <option value="todos">Todos</option>
            <option value="0">Vencidas (0)</option>
            <option value="1-3">1–3 días</option>
            <option value="4-7">4–7 días</option>
            <option value="8+">8 o más</option>
            {draft.dias === '1-7' && <option value="1-7">1–7 días</option>}
            <option value="custom">Personalizado</option>
          </select>

          {draft.dias === 'custom' && (
            <div className="v04-popover-row">
              <div>
                <label htmlFor="filtro-dias-min" className="v04-popover-sublabel">
                  Mínimo
                </label>
                <input
                  id="filtro-dias-min"
                  type="text"
                  inputMode="numeric"
                  placeholder="0"
                  className="solicitud-input"
                  value={draft.diasMin}
                  aria-invalid={Boolean(errors.diasCustom)}
                  onChange={(event) => onUpdateField('diasMin', event.target.value)}
                />
              </div>

              <div>
                <label htmlFor="filtro-dias-max" className="v04-popover-sublabel">
                  Máximo
                </label>
                <input
                  id="filtro-dias-max"
                  type="text"
                  inputMode="numeric"
                  placeholder="999"
                  className="solicitud-input"
                  value={draft.diasMax}
                  aria-invalid={Boolean(errors.diasCustom)}
                  onChange={(event) => onUpdateField('diasMax', event.target.value)}
                />
              </div>
            </div>
          )}

          {errors.diasCustom && <p className="solicitud-field-error">{errors.diasCustom}</p>}
        </div>

        <div className="v04-popover-actions">
          <button
            type="button"
            className="solicitud-btn solicitud-btn-secondary"
            onClick={onClear}
          >
            Limpiar
          </button>

          <button type="submit" className="solicitud-btn solicitud-btn-primary">
            Aplicar
          </button>
        </div>
      </form>
    </div>
  );
}
