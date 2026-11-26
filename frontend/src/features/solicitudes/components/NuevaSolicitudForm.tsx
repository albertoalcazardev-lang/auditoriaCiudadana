import { AlertCircle, Calendar, Clock, RefreshCw, Save, Sparkles } from 'lucide-react';
import { HttpClientError } from '../../../core/api/httpClient';
import type { useNuevaSolicitudViewModel } from '../hooks/useNuevaSolicitudViewModel';
import { AreaReceptoraCombobox } from './AreaReceptoraCombobox';

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
  const asuntoLength = values.asunto.length;
  const asuntoCounterClass =
    asuntoLength > 100
      ? 'solicitud-counter is-error'
      : asuntoLength >= 90
        ? 'solicitud-counter is-warning'
        : 'solicitud-counter';

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

      {serverBannerError && (
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

          {/* Fila 1: Fecha de entrega | Hora de entrega */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-fecha-entrega" className="solicitud-label">
              Fecha de entrega
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <div className="solicitud-input-wrap">
              <input
                ref={registerFieldRef('fechaEntrega')}
                id="solicitud-fecha-entrega"
                name="fechaEntrega"
                type="text"
                inputMode="numeric"
                className="solicitud-input has-right-icon"
                placeholder="dd/mm/aaaa"
                value={values.fechaEntrega}
                disabled={controlsDisabled}
                aria-invalid={Boolean(visibleErrors.fechaEntrega)}
                aria-describedby={visibleErrors.fechaEntrega ? 'error-fecha-entrega' : undefined}
                onChange={(event) => updateField('fechaEntrega', event.target.value)}
                onBlur={() => handleFieldBlur('fechaEntrega')}
              />
              <Calendar size={16} className="solicitud-input-icon" aria-hidden="true" />
            </div>
            {visibleErrors.fechaEntrega && (
              <p id="error-fecha-entrega" className="solicitud-field-error">
                {visibleErrors.fechaEntrega}
              </p>
            )}
          </div>

          <div className="solicitud-field">
            <label htmlFor="solicitud-hora-entrega" className="solicitud-label">
              Hora de entrega
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <div className="solicitud-input-wrap">
              <input
                ref={registerFieldRef('horaEntrega')}
                id="solicitud-hora-entrega"
                name="horaEntrega"
                type="text"
                inputMode="numeric"
                className="solicitud-input has-right-icon"
                placeholder="HH:mm"
                value={values.horaEntrega}
                disabled={controlsDisabled}
                aria-invalid={Boolean(visibleErrors.horaEntrega)}
                aria-describedby={visibleErrors.horaEntrega ? 'error-hora-entrega' : undefined}
                onChange={(event) => updateField('horaEntrega', event.target.value)}
                onBlur={() => handleFieldBlur('horaEntrega')}
              />
              <Clock size={16} className="solicitud-input-icon" aria-hidden="true" />
            </div>
            {visibleErrors.horaEntrega && (
              <p id="error-hora-entrega" className="solicitud-field-error">
                {visibleErrors.horaEntrega}
              </p>
            )}
          </div>

          {/* Fila 2: Área o dependencia receptora (ancho completo) */}
          <div className="solicitud-field solicitud-col-full">
            <label htmlFor="solicitud-area-receptora" className="solicitud-label">
              Área o dependencia receptora
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <AreaReceptoraCombobox
              id="solicitud-area-receptora"
              options={catalogos?.areasReceptoras ?? []}
              value={values.areaReceptoraId}
              disabled={controlsDisabled}
              error={visibleErrors.areaReceptoraId}
              errorId="error-area-receptora"
              inputRef={registerFieldRef('areaReceptoraId')}
              onChange={(areaId) => updateField('areaReceptoraId', areaId)}
              onBlur={() => handleFieldBlur('areaReceptoraId')}
            />
            {visibleErrors.areaReceptoraId && (
              <p id="error-area-receptora" className="solicitud-field-error">
                {visibleErrors.areaReceptoraId}
              </p>
            )}
          </div>

          {/* Fila 3: Asunto corto (ancho completo) */}
          <div className="solicitud-field solicitud-col-full">
            <label htmlFor="solicitud-asunto" className="solicitud-label">
              Asunto corto
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <input
              ref={registerFieldRef('asunto')}
              id="solicitud-asunto"
              name="asunto"
              type="text"
              className="solicitud-input"
              placeholder="Describe brevemente la solicitud"
              value={values.asunto}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.asunto)}
              aria-describedby={
                visibleErrors.asunto ? 'error-asunto asunto-counter' : 'asunto-counter'
              }
              onChange={(event) => updateField('asunto', event.target.value)}
              onBlur={() => handleFieldBlur('asunto')}
            />
            <div className="solicitud-asunto-footer">
              {visibleErrors.asunto && (
                <p id="error-asunto" className="solicitud-field-error">
                  {visibleErrors.asunto}
                </p>
              )}
              <span id="asunto-counter" className={asuntoCounterClass}>
                {`${asuntoLength}/100`}
              </span>
            </div>
          </div>

          {/* Fila 4: Medio de entrega | Estatus de entrega */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-medio-entrega" className="solicitud-label">
              Medio de entrega
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <select
              ref={registerFieldRef('medioEntrega')}
              id="solicitud-medio-entrega"
              name="medioEntrega"
              className="solicitud-select"
              value={values.medioEntrega}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.medioEntrega)}
              aria-describedby={visibleErrors.medioEntrega ? 'error-medio-entrega' : undefined}
              onChange={(event) => updateField('medioEntrega', event.target.value)}
              onBlur={() => handleFieldBlur('medioEntrega')}
            >
              <option value="">Selecciona una opción</option>
              {(catalogos?.mediosEntrega ?? []).map((item) => (
                <option key={item.codigo} value={item.codigo}>
                  {item.nombre}
                </option>
              ))}
            </select>
            {visibleErrors.medioEntrega && (
              <p id="error-medio-entrega" className="solicitud-field-error">
                {visibleErrors.medioEntrega}
              </p>
            )}
          </div>

          <div className="solicitud-field">
            <label htmlFor="solicitud-estatus-entrega" className="solicitud-label">
              Estatus de entrega
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <select
              ref={registerFieldRef('estatusEntrega')}
              id="solicitud-estatus-entrega"
              name="estatusEntrega"
              className="solicitud-select"
              value={values.estatusEntrega}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.estatusEntrega)}
              aria-describedby={visibleErrors.estatusEntrega ? 'error-estatus-entrega' : undefined}
              onChange={(event) => updateField('estatusEntrega', event.target.value)}
              onBlur={() => handleFieldBlur('estatusEntrega')}
            >
              <option value="">Selecciona una opción</option>
              {selectableEstatusEntrega.map((item) => (
                <option key={item.codigo} value={item.codigo}>
                  {item.nombre}
                </option>
              ))}
            </select>
            {visibleErrors.estatusEntrega && (
              <p id="error-estatus-entrega" className="solicitud-field-error">
                {visibleErrors.estatusEntrega}
              </p>
            )}
          </div>

          {/* Fila 5: Tipo de solicitud | Evidencia */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-tipo-solicitud" className="solicitud-label">
              Tipo de solicitud
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <select
              ref={registerFieldRef('tipoSolicitud')}
              id="solicitud-tipo-solicitud"
              name="tipoSolicitud"
              className="solicitud-select"
              value={values.tipoSolicitud}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.tipoSolicitud)}
              aria-describedby={visibleErrors.tipoSolicitud ? 'error-tipo-solicitud' : undefined}
              onChange={(event) => updateField('tipoSolicitud', event.target.value)}
              onBlur={() => handleFieldBlur('tipoSolicitud')}
            >
              <option value="">Selecciona una opción</option>
              {(catalogos?.tiposSolicitud ?? []).map((item) => (
                <option key={item.codigo} value={item.codigo}>
                  {item.nombre}
                </option>
              ))}
            </select>
            {visibleErrors.tipoSolicitud && (
              <p id="error-tipo-solicitud" className="solicitud-field-error">
                {visibleErrors.tipoSolicitud}
              </p>
            )}
          </div>

          <div className="solicitud-field">
            <label htmlFor="solicitud-tipo-evidencia" className="solicitud-label">
              Evidencia que se lleva a cabo
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <select
              ref={registerFieldRef('tipoEvidencia')}
              id="solicitud-tipo-evidencia"
              name="tipoEvidencia"
              className="solicitud-select"
              value={values.tipoEvidencia}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.tipoEvidencia)}
              aria-describedby={visibleErrors.tipoEvidencia ? 'error-tipo-evidencia' : undefined}
              onChange={(event) => updateField('tipoEvidencia', event.target.value)}
              onBlur={() => handleFieldBlur('tipoEvidencia')}
            >
              <option value="">Selecciona una opción</option>
              {(catalogos?.tiposEvidencia ?? []).map((item) => (
                <option key={item.codigo} value={item.codigo}>
                  {item.nombre}
                </option>
              ))}
            </select>
            {visibleErrors.tipoEvidencia && (
              <p id="error-tipo-evidencia" className="solicitud-field-error">
                {visibleErrors.tipoEvidencia}
              </p>
            )}
          </div>

          {/* Fila 6: ¿Tiene acuse? | Tipo de acuse */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-tiene-acuse" className="solicitud-label">
              ¿Tiene acuse?
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <select
              ref={registerFieldRef('tieneAcuse')}
              id="solicitud-tiene-acuse"
              name="tieneAcuse"
              className="solicitud-select"
              value={values.tieneAcuse}
              disabled={controlsDisabled}
              aria-invalid={Boolean(visibleErrors.tieneAcuse)}
              aria-describedby={visibleErrors.tieneAcuse ? 'error-tiene-acuse' : undefined}
              onChange={(event) =>
                handleTieneAcuseChange(event.target.value as '' | 'si' | 'no')
              }
              onBlur={() => handleFieldBlur('tieneAcuse')}
            >
              <option value="">Selecciona una opción</option>
              <option value="si">Sí</option>
              <option value="no">No</option>
            </select>
            {visibleErrors.tieneAcuse && (
              <p id="error-tiene-acuse" className="solicitud-field-error">
                {visibleErrors.tieneAcuse}
              </p>
            )}
          </div>

          <div className="solicitud-field">
            <label htmlFor="solicitud-tipo-acuse" className="solicitud-label">
              Tipo de acuse
              {values.tieneAcuse === 'si' && (
                <span className="solicitud-required" aria-hidden="true">
                  *
                </span>
              )}
            </label>
            <select
              ref={registerFieldRef('tipoAcuse')}
              id="solicitud-tipo-acuse"
              name="tipoAcuse"
              className="solicitud-select"
              value={values.tipoAcuse}
              disabled={controlsDisabled || values.tieneAcuse !== 'si'}
              aria-invalid={Boolean(visibleErrors.tipoAcuse)}
              aria-describedby={visibleErrors.tipoAcuse ? 'error-tipo-acuse' : undefined}
              onChange={(event) => updateField('tipoAcuse', event.target.value)}
              onBlur={() => handleFieldBlur('tipoAcuse')}
            >
              <option value="">Selecciona una opción</option>
              {selectableTiposAcuse.map((item) => (
                <option key={item.codigo} value={item.codigo}>
                  {item.nombre}
                </option>
              ))}
            </select>
            {visibleErrors.tipoAcuse && (
              <p id="error-tipo-acuse" className="solicitud-field-error">
                {visibleErrors.tipoAcuse}
              </p>
            )}
          </div>

          {/* Fila 7: Plazo de vencimiento | Días restantes (solo lectura) */}
          <div className="solicitud-field">
            <label htmlFor="solicitud-fecha-vencimiento" className="solicitud-label">
              Plazo de vencimiento
              <span className="solicitud-required" aria-hidden="true">
                *
              </span>
            </label>
            <div className="solicitud-input-wrap">
              <input
                ref={registerFieldRef('fechaVencimiento')}
                id="solicitud-fecha-vencimiento"
                name="fechaVencimiento"
                type="text"
                inputMode="numeric"
                className="solicitud-input has-right-icon"
                placeholder="dd/mm/aaaa"
                value={values.fechaVencimiento}
                disabled={controlsDisabled}
                aria-invalid={Boolean(visibleErrors.fechaVencimiento)}
                aria-describedby={
                  visibleErrors.fechaVencimiento ? 'error-fecha-vencimiento' : undefined
                }
                onChange={(event) => updateField('fechaVencimiento', event.target.value)}
                onBlur={() => handleFieldBlur('fechaVencimiento')}
              />
              <Calendar size={16} className="solicitud-input-icon" aria-hidden="true" />
            </div>
            {visibleErrors.fechaVencimiento && (
              <p id="error-fecha-vencimiento" className="solicitud-field-error">
                {visibleErrors.fechaVencimiento}
              </p>
            )}
          </div>

          <div className="solicitud-field">
            <span id="solicitud-dias-restantes-label" className="solicitud-label">
              Días restantes
            </span>
            <div
              role="status"
              aria-labelledby="solicitud-dias-restantes-label"
              data-tone={diasRestantesTone}
              className={`solicitud-dias-control is-${diasRestantesTone}`}
            >
              <span>
                {diasRestantes === null
                  ? 'Días restantes: —'
                  : `Días restantes: ${diasRestantes} ${diasRestantes === 1 ? 'día' : 'días'}`}
              </span>
            </div>
          </div>
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
