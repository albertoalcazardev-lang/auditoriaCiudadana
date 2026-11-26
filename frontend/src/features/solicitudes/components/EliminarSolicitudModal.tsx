import { useEffect, useId, useRef } from 'react';
import { AlertTriangle } from 'lucide-react';

export interface EliminarSolicitudModalProps {
  open: boolean;
  folio: string;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

const FOCUSABLE_SELECTOR =
  'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])';

export function EliminarSolicitudModal({
  open,
  folio,
  busy = false,
  onCancel,
  onConfirm,
}: EliminarSolicitudModalProps) {
  const titleId = useId();
  const descId = useId();
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const cancelBtnRef = useRef<HTMLButtonElement | null>(null);
  const previousActiveRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previousActiveRef.current = document.activeElement as HTMLElement | null;
    const frameId = requestAnimationFrame(() => {
      if (cancelBtnRef.current) {
        cancelBtnRef.current.focus();
      } else {
        dialogRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
      }
    });

    return () => {
      cancelAnimationFrame(frameId);
      if (previousActiveRef.current && document.contains(previousActiveRef.current)) {
        previousActiveRef.current.focus();
      }
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        if (!busy) {
          event.preventDefault();
          onCancel();
        }
        return;
      }

      if (event.key !== 'Tab' || !dialogRef.current) return;

      const focusable = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      );
      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement as HTMLElement | null;

      if (event.shiftKey) {
        if (!active || active === first || !dialogRef.current.contains(active)) {
          event.preventDefault();
          last.focus();
        }
      } else if (!active || active === last || !dialogRef.current.contains(active)) {
        event.preventDefault();
        first.focus();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [busy, onCancel, open]);

  if (!open) return null;

  return (
    <div className="solicitud-modal-overlay">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-busy={busy}
        aria-labelledby={titleId}
        aria-describedby={descId}
        className="v04-delete-modal"
      >
        <div className="v04-delete-modal-icon" aria-hidden="true">
          <AlertTriangle size={26} />
        </div>

        <h2 id={titleId} className="v04-delete-modal-title">
          ¿Eliminar solicitud?
        </h2>

        <p id={descId} className="v04-delete-modal-message">
          Esta acción no se puede deshacer. ¿Está seguro de que desea eliminar la solicitud{' '}
          <strong>{folio}</strong>?
        </p>

        <div className="v04-delete-modal-actions">
          <button
            ref={cancelBtnRef}
            type="button"
            disabled={busy}
            className="solicitud-btn solicitud-btn-secondary"
            onClick={onCancel}
          >
            Cancelar
          </button>

          <button
            type="button"
            disabled={busy}
            className="solicitud-btn v04-btn-delete-danger"
            onClick={onConfirm}
          >
            {busy ? 'Eliminando…' : 'Eliminar'}
          </button>
        </div>
      </div>
    </div>
  );
}
