import { AlertCircle, RefreshCw, Save, Sparkles } from 'lucide-react';
import { HttpClientError } from '../../../core/api/httpClient';
import type { useNuevaSolicitudViewModel } from '../hooks/useNuevaSolicitudViewModel';
import { SolicitudCamposEditablesGrid } from './SolicitudCamposEditablesGrid';

type ViewModel = ReturnType<typeof useNuevaSolicitudViewModel>;

interface NuevaSolicitudFormProps {
  vm: ViewModel;
}

export function NuevaSolicitudForm({ vm }: NuevaSolicitudFormProps) {
  const {
    catalogosQuery,
    selectableEstatusEntrega,
    selectableTiposAcuse,
    values,
    folioDisplayValue,
    diasRestantes,
    diasRestantesTone,
    visibleErrors,
    invalidCount,
    showValidationSummary,
    serverBannerError,
    submitStatus,
    isFormLocked,
    isUncertain,
    registerFieldRef,
    updateField,
    handleFieldBlur,
    toggleFolioAutomatico,
    handleTieneAcuseChange,
    requestSubmit,
    retryUncertainSubmission,
    requestCancel,
  } = vm;

  const catalogos = catalogosQuery.data;
  const controlsDisabled = isFormLocked || !catalogos;

  const folioBadgeText = values.generarFolioAutomatico
    ? '# Folio: se asignará al guardar'
    : values.folio.trim()
      ? `# Folio: ${values.folio.trim()}`
      : '# Folio: captura manual';

  return (
    <section className="solicitud-card" aria-labelledby="form-info-general">
      {catalogosQuery.isPending && (
        <div role="status" className="solicitud-banner solicitud-banner-info">
          <div className="solicitud-banner-content">
            <span>Cargando catálogos de solicitudes…</span>
          </div>
        </div>
      )}

      {catalogosQuery.isError && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>
              {catalogosQuery.error instanceof HttpClientError &&
              catalogosQuery.error.statusCode === 401
                ? 'Tu sesión expiró. Inicia sesión nuevamente para continuar.'
                : catalogosQuery.error instanceof HttpClientError &&
                    catalogosQuery.error.statusCode === 403
                  ? 'No tienes permisos para consultar los catálogos.'
                  : 'No fue posible cargar los catálogos. Intenta nuevamente.'}
            </span>
          </div>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-secondary"
            onClick={() => void catalogosQuery.refetch()}
          >
            <RefreshCw size={15} aria-hidden="true" />
            Reintentar
          </button>
        </div>
      )}

      {isUncertain && (
        <div role="alert" className="solicitud-banner solicitud-banner-error">
          <div className="solicitud-banner-content">
            <AlertCircle size={18} aria-hidden="true" />
            <span>
              No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo
              {serverBannerError ? ` (${serverBannerError})` : ''}
            </span>
          </div>
          <button
            type="button"
            className="solicitud-btn solicitud-btn-primary"
            disabled={submitStatus === 'submitting'}
            onClick={retryUncertainSubmission}
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
        <span className="solicitud-folio-badge">{folioBadgeText}</span>
      </div>

      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          requestSubmit();
        }}
      >
        <div className="solicitud-grid">
          {/* Fila 0: Folio | Botón Generar folio automático */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-folio" className="solicitud-label">
              Folio
              {!values.generarFolioAutomatico && (
                <span className="solicitud-required" aria-hidden="true">
                  *
                </span>
              )}
            </label>
            <input
              ref={registerFieldRef('folio')}
              id="solicitud-folio"
              name="folio"
              type="text"
              className="solicitud-input"
              placeholder="Ingresa el folio de la solicitud"
              value={folioDisplayValue}
              disabled={controlsDisabled || values.generarFolioAutomatico}
              aria-invalid={Boolean(visibleErrors.folio)}
              aria-describedby={visibleErrors.folio ? 'error-folio' : undefined}
              onChange={(event) => updateField('folio', event.target.value)}
              onBlur={() => handleFieldBlur('folio')}
            />
            {visibleErrors.folio && (
              <p id="error-folio" className="solicitud-field-error">
                {visibleErrors.folio}
              </p>
            )}
          </div>

          <div className="solicitud-folio-action-cell">
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              disabled={controlsDisabled}
              onClick={toggleFolioAutomatico}
            >
              <Sparkles size={16} aria-hidden="true" />
              {values.generarFolioAutomatico
                ? 'Capturar folio manual'
                : 'Generar folio automático'}
            </button>
          </div>

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

        <p className="solicitud-mobile-required-note">* Campos obligatorios</p>

        <div className="solicitud-actions-bar">
          <span className="solicitud-footer-note">* Campos obligatorios</span>

          <div className="solicitud-actions-buttons">
            <button
              type="button"
              className="solicitud-btn solicitud-btn-secondary"
              disabled={submitStatus === 'submitting'}
              onClick={requestCancel}
            >
              Cancelar
            </button>

            <button
              type="submit"
              className="solicitud-btn solicitud-btn-primary"
              disabled={controlsDisabled}
            >
              <Save size={16} aria-hidden="true" />
              {submitStatus === 'submitting' ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </div>
      </form>
    </section>
  );
}
