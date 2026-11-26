import { useEffect, useId, useRef, type ReactNode } from 'react';

export interface ModalAction {
  label: string;
  variant: 'primary' | 'secondary' | 'danger';
  onClick: () => void;
  initialFocus?: boolean;
  disabled?: boolean;
}

interface ConfirmacionModalProps {
  open: boolean;
  title: string;
  message: string;
  detail?: ReactNode;
  busy?: boolean;
  onClose: () => void;
  actions: ModalAction[];
}

const FOCUSABLE_SELECTOR =
  'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])';

export function ConfirmacionModal({
  open,
  title,
  message,
  detail,
  busy = false,
  onClose,
  actions,
}: ConfirmacionModalProps) {
  const titleId = useId();
  const descId = useId();
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const initialFocusRef = useRef<HTMLButtonElement | null>(null);
  const previousActiveRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previousActiveRef.current = document.activeElement as HTMLElement | null;
    const frameId = requestAnimationFrame(() => {
      if (initialFocusRef.current) {
        initialFocusRef.current.focus();
      } else {
        const firstFocusable = dialogRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR);
        firstFocusable?.focus();
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
          onClose();
        }
        return;
      }

      if (event.key !== 'Tab' || !dialogRef.current) return;

      const focusableElements = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      );
      if (focusableElements.length === 0) return;

      const first = focusableElements[0];
      const last = focusableElements[focusableElements.length - 1];
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
  }, [busy, onClose, open]);

  if (!open) return null;

  return (
    <div className="solicitud-modal-overlay">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        className="solicitud-modal"
      >
        <h2 id={titleId} className="solicitud-modal-title">
          {title}
        </h2>
        <p id={descId} className="solicitud-modal-message">
          {message}
        </p>

        {detail && <div className="solicitud-modal-detail">{detail}</div>}

        <div className="solicitud-modal-actions">
          {actions.map((action) => {
            const variantClass =
              action.variant === 'primary'
                ? 'solicitud-btn-primary'
                : action.variant === 'danger'
                  ? 'solicitud-btn-danger'
                  : 'solicitud-btn-secondary';
            return (
              <button
                key={action.label}
                ref={action.initialFocus ? initialFocusRef : undefined}
                type="button"
                disabled={busy || action.disabled}
                className={`solicitud-btn ${variantClass}`}
                onClick={action.onClick}
              >
                {action.label}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
