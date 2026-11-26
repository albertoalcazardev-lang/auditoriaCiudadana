import { AlertCircle, Hash, RefreshCw, Save, Trash2 } from 'lucide-react';
import type { useEditarSolicitudViewModel } from '../hooks/useEditarSolicitudViewModel';
import { SolicitudCamposEditablesGrid } from './SolicitudCamposEditablesGrid';

type EditarViewModel = ReturnType<typeof useEditarSolicitudViewModel>;

interface EditarSolicitudFormProps {
  vm: EditarViewModel;
}

const SKELETON_FIELDS: ReadonlyArray<{ label: string; fullWidth?: boolean }> = [
  { label: 'Fecha de entrega' },
  { label: 'Hora de entrega' },
  { label: 'Área o dependencia receptora', fullWidth: true },
  { label: 'Asunto corto', fullWidth: true },
  { label: 'Medio de entrega' },
  { label: 'Estatus de entrega' },
  { label: 'Tipo de solicitud' },
  { label: 'Evidencia que se lleva a cabo' },
  { label: '¿Tiene acuse?' },
  { label: 'Tipo de acuse' },
  { label: 'Plazo de vencimiento' },
  { label: 'Días restantes' },
];

export function EditarSolicitudForm({ vm }: EditarSolicitudFormProps) {
  const {
    folioReal,
    catalogos,
    isPreloading,
    isHydrated,
    initialLoadErrorType,
    selectableEstatusEntrega,
    selectableTiposAcuse,
    values,
    diasRestantes,
    diasRestantesTone,
    visibleErrors,
    invalidCount,
    showValidationSummary,
    serverBannerError,
    submitStatus,
    isFormLocked,
    isUncertain,
    isSaveDisabled,
    isConcurrencyStale,
    isReloadingDatos,
    reloadError,
    registerFieldRef,
    updateField,
    handleFieldBlur,
    handleTieneAcuseChange,
    retryInitialLoad,
    requestSubmit,
    retryUncertainUpdate,
    reloadAfterConflict,
    requestCancel,
  } = vm;

  const controlsDisabled = isFormLocked || !catalogos;

  return (
    <section className="solicitud-card" aria-labelledby="form-info-general">
      {/* Banner B-04: Falla de carga inicial */}
      {initialLoadErrorType === 'b04' && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>No se pudo cargar la solicitud.</span>
          </div>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-secondary"
            onClick={retryInitialLoad}
          >
            <RefreshCw size={15} aria-hidden="true" />
            Reintentar
          </button>
        </div>
      )}

      {initialLoadErrorType === 'unauthorized' && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>Tu sesión expiró. Inicia sesión nuevamente para continuar.</span>
          </div>
        </div>
      )}

      {initialLoadErrorType === 'forbidden' && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>No tienes permisos para editar esta solicitud.</span>
          </div>
        </div>
      )}

      {/* Banner de obsolescencia tras cerrar M-05 */}
      {isHydrated && isConcurrencyStale && (
        <div role="alert" className="solicitud-banner solicitud-banner-warning">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>
              Recarga los datos para continuar.
              {reloadError ? ` ${reloadError}` : ''}
            </span>
          </div>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-primary"
            disabled={isReloadingDatos}
            onClick={() => void reloadAfterConflict()}
          >
            <RefreshCw size={15} aria-hidden="true" />
            {isReloadingDatos ? 'Recargando…' : 'Recargar datos'}
          </button>
        </div>
      )}

      {/* Banner B-03: Falla de persistencia / respuesta perdida */}
      {isUncertain && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>
              No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo.
              {serverBannerError ? ` (${serverBannerError})` : ''}
            </span>
          </div>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-primary"
            disabled={submitStatus === 'submitting'}
            onClick={retryUncertainUpdate}
          >
            <RefreshCw size={15} aria-hidden="true" />
            Reintentar
          </button>
        </div>
      )}

      {!isUncertain && serverBannerError && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>{serverBannerError}</span>
          </div>
        </div>
      )}

      {showValidationSummary && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>{`Revisa los campos marcados (${invalidCount})`}</span>
          </div>
        </div>
      )}

      <div className="solicitud-card-header">
        <h2 id="form-info-general" className="solicitud-card-title">
          Información general
        </h2>
        <span className="solicitud-folio-chip-immutable" data-testid="immutable-folio-chip">
          <Hash size={14} aria-hidden="true" />
          <span>{`Folio ${folioReal}`}</span>
        </span>
      </div>

      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          requestSubmit();
        }}
      >
        {isPreloading && (
          <div
            role="status"
            aria-busy="true"
            aria-label="Cargando solicitud para edición"
            className="solicitud-grid"
          >
            {SKELETON_FIELDS.map((item) => (
              <div
                key={item.label}
                className={`solicitud-field ${item.fullWidth ? 'solicitud-col-full' : ''}`}
              >
                <span className="solicitud-label">{item.label}</span>
                <div
                  data-testid="solicitud-skeleton-control"
                  className="solicitud-skeleton-control"
                />
              </div>
            ))}
          </div>
        )}

        {isHydrated && (
          <div className="solicitud-grid">
            <SolicitudCamposEditablesGrid
              values={values}
              catalogos={catalogos}
              selectableEstatusEntrega={selectableEstatusEntrega}
              selectableTiposAcuse={selectableTiposAcuse}
              diasRestantes={diasRestantes}
              diasRestantesTone={diasRestantesTone}
              visibleErrors={visibleErrors}
              controlsDisabled={controlsDisabled}
              registerFieldRef={registerFieldRef}
              updateField={updateField}
              handleFieldBlur={handleFieldBlur}
              handleTieneAcuseChange={handleTieneAcuseChange}
            />
          </div>
        )}

        <p className="solicitud-mobile-required-note">* Campos obligatorios</p>

        <div className="solicitud-actions-bar">
          <span className="solicitud-footer-note">* Campos obligatorios</span>

          <div className="solicitud-actions-buttons">
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              disabled={submitStatus === 'submitting' || isReloadingDatos}
              onClick={requestCancel}
            >
              Cancelar
            </button>

            <button
              type="button"
              className="solicitud-btn solicitud-btn-danger-outline"
              disabled
              aria-disabled="true"
            >
              <Trash2 size={16} aria-hidden="true" />
              Eliminar
            </button>

            <button
              type="submit"
              className="solicitud-btn solicitud-btn-primary"
              disabled={isSaveDisabled}
            >
              <Save size={16} aria-hidden="true" />
              {submitStatus === 'submitting' ? 'Guardando cambios…' : 'Guardar cambios'}
            </button>
          </div>
        </div>
      </form>
    </section>
  );
}
