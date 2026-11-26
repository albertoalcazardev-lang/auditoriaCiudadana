import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { HttpClientError } from '../../../core/api/httpClient';
import { clearCsrfToken } from '../../auth/api/authApi';
import { sessionKey } from '../../auth/hooks/useSessionViewModel';
import { crearSolicitud, fetchSolicitudesCatalogos } from '../api/solicitudesApi';
import type {
  CrearSolicitudPayload,
  SolicitudConfirmacionResumen,
  SolicitudFieldErrors,
  SolicitudFieldName,
  SolicitudFormValues,
  SolicitudModalState,
  SolicitudesCatalogos,
  SubmitStatus,
} from '../types/solicitudes.types';
import {
  computeDiasRestantesMexicoCity,
  getDiasRestantesTone,
  getMexicoCityNowParts,
} from './solicitudesDateUtils';
import {
  CREATE_FIELD_ORDER,
  CREATE_SERVER_FIELD_MAP,
  applyTieneAcuseChange,
  buildActualizarSolicitudPayload,
  buildSolicitudConfirmacionResumen,
  getSelectableEstatusEntrega,
  getSelectableTiposAcuse,
  mapValidationProblemDetails,
  validateEditableSolicitudFields,
} from './solicitudesFormUtils';

const FOLIO_POLICY_REGEX = /^(?=.*[A-Za-z0-9])[A-Za-z0-9._/-]{1,100}$/;

function createInitialFormValues(): SolicitudFormValues {
  const nowMx = getMexicoCityNowParts();
  return {
    generarFolioAutomatico: false,
    folio: '',
    fechaEntrega: nowMx.fechaFormatted,
    horaEntrega: nowMx.horaFormatted,
    areaReceptoraId: '',
    asunto: '',
    medioEntrega: '',
    estatusEntrega: '',
    tipoSolicitud: '',
    tipoEvidencia: '',
    tieneAcuse: '',
    tipoAcuse: '',
    fechaVencimiento: '',
  };
}

export function validateSolicitudForm(
  values: SolicitudFormValues,
  catalogos?: SolicitudesCatalogos,
): SolicitudFieldErrors {
  const errors: SolicitudFieldErrors = {
    ...validateEditableSolicitudFields(values, catalogos),
  };

  if (!values.generarFolioAutomatico) {
    const trimmedFolio = values.folio.trim();
    if (!trimmedFolio) {
      errors.folio = 'Ingresa el folio de la solicitud.';
    } else if (trimmedFolio.length > 100) {
      errors.folio = 'El folio no puede exceder 100 caracteres.';
    } else if (!FOLIO_POLICY_REGEX.test(trimmedFolio)) {
      errors.folio =
        'El folio solo acepta letras sin acentos, dígitos y separadores -, /, ., _, con al menos un carácter alfanumérico.';
    }
  }

  return errors;
}

const UNCERTAIN_SUBMISSION_STORAGE_KEY = 'vyt.solicitudes.uncertainSubmission';

interface PendingSubmissionRecord {
  ownerUserId: string;
  idempotencyKey: string;
  payload: CrearSolicitudPayload;
  values: SolicitudFormValues;
  manualFolioDraft: string;
  wasUncertain: boolean;
}

function readUncertainSubmission(): PendingSubmissionRecord | null {
  try {
    const raw = window.sessionStorage.getItem(UNCERTAIN_SUBMISSION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<PendingSubmissionRecord> | null;
    if (
      !parsed ||
      typeof parsed.ownerUserId !== 'string' ||
      typeof parsed.idempotencyKey !== 'string' ||
      !parsed.payload ||
      typeof parsed.payload !== 'object' ||
      !parsed.values ||
      typeof parsed.values !== 'object' ||
      typeof parsed.manualFolioDraft !== 'string'
    ) {
      window.sessionStorage.removeItem(UNCERTAIN_SUBMISSION_STORAGE_KEY);
      return null;
    }
    return {
      ownerUserId: parsed.ownerUserId,
      idempotencyKey: parsed.idempotencyKey,
      payload: parsed.payload,
      values: parsed.values,
      manualFolioDraft: parsed.manualFolioDraft,
      wasUncertain: true,
    };
  } catch {
    return null;
  }
}

function saveUncertainSubmission(record: PendingSubmissionRecord): void {
  try {
    window.sessionStorage.setItem(
      UNCERTAIN_SUBMISSION_STORAGE_KEY,
      JSON.stringify({
        ownerUserId: record.ownerUserId,
        idempotencyKey: record.idempotencyKey,
        payload: record.payload,
        values: record.values,
        manualFolioDraft: record.manualFolioDraft,
      }),
    );
  } catch {
    // Ignorar errores de almacenamiento restringido; el estado en memoria sigue activo.
  }
}

function clearUncertainSubmission(): void {
  try {
    window.sessionStorage.removeItem(UNCERTAIN_SUBMISSION_STORAGE_KEY);
  } catch {
    // Ignorar errores de almacenamiento restringido.
  }
}

function getErrorCode(details: unknown): string | undefined {
  if (!details || typeof details !== 'object') return undefined;
  const code = (details as { code?: unknown }).code;
  return typeof code === 'string' ? code : undefined;
}

export function useNuevaSolicitudViewModel(userId: string) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [initialBootstrap] = useState(() => {
    const defaults = createInitialFormValues();
    const saved = readUncertainSubmission();
    if (saved && saved.ownerUserId !== userId) {
      clearUncertainSubmission();
    }
    const restored = saved && saved.ownerUserId === userId ? saved : null;
    return { defaults, restored };
  });

  const [initialDefaults, setInitialDefaults] = useState<SolicitudFormValues>(
    () => initialBootstrap.defaults,
  );
  const [formState, setFormState] = useState<{
    values: SolicitudFormValues;
    manualFolioDraft: string;
  }>(() =>
    initialBootstrap.restored
      ? {
          values: initialBootstrap.restored.values,
          manualFolioDraft: initialBootstrap.restored.manualFolioDraft,
        }
      : {
          values: initialDefaults,
          manualFolioDraft: '',
        },
  );
  const { values } = formState;
  const [touchedFields, setTouchedFields] = useState<Partial<Record<SolicitudFieldName, boolean>>>(
    {},
  );
  const [hasAttemptedSubmit, setHasAttemptedSubmit] = useState(false);
  const [serverFieldErrors, setServerFieldErrors] = useState<SolicitudFieldErrors>({});
  const [serverBannerError, setServerBannerError] = useState<string | null>(null);
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>(() =>
    initialBootstrap.restored ? 'uncertain' : 'idle',
  );
  const [modalState, setModalState] = useState<SolicitudModalState>('none');
  const [confirmacion, setConfirmacion] = useState<SolicitudConfirmacionResumen | null>(null);
  const [focusRequest, setFocusRequest] = useState<{
    field: SolicitudFieldName;
    seq: number;
  } | null>(null);

  const pendingSubmissionRef = useRef<PendingSubmissionRecord | null>(initialBootstrap.restored);
  const allowExitRef = useRef(false);
  const fieldRefs = useRef<Partial<Record<SolicitudFieldName, HTMLElement | null>>>({});

  useEffect(() => {
    if (pendingSubmissionRef.current && pendingSubmissionRef.current.ownerUserId !== userId) {
      clearUncertainSubmission();
      pendingSubmissionRef.current = null;
      const freshDefaults = createInitialFormValues();
      setInitialDefaults(freshDefaults);
      setFormState({ values: freshDefaults, manualFolioDraft: '' });
      setServerFieldErrors({});
      setServerBannerError(null);
      setModalState('none');
      setSubmitStatus('idle');
      return;
    }

    const saved = readUncertainSubmission();
    if (saved && saved.ownerUserId !== userId) {
      clearUncertainSubmission();
    }
  }, [userId]);

  const catalogosQuery = useQuery({
    queryKey: ['solicitudes', 'catalogos'],
    queryFn: async () => {
      try {
        return await fetchSolicitudesCatalogos();
      } catch (error) {
        if (error instanceof HttpClientError && error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
        }
        throw error;
      }
    },
    retry: false,
  });

  const selectableEstatusEntrega = useMemo(
    () => getSelectableEstatusEntrega(catalogosQuery.data),
    [catalogosQuery.data],
  );

  const selectableTiposAcuse = useMemo(
    () => getSelectableTiposAcuse(catalogosQuery.data, values.tieneAcuse),
    [catalogosQuery.data, values.tieneAcuse],
  );

  const diasRestantes = useMemo(
    () => computeDiasRestantesMexicoCity(values.fechaVencimiento),
    [values.fechaVencimiento],
  );

  const diasRestantesTone = useMemo(() => getDiasRestantesTone(diasRestantes), [diasRestantes]);

  const validationErrors = useMemo(
    () => validateSolicitudForm(values, catalogosQuery.data),
    [catalogosQuery.data, values],
  );

  const visibleErrors: SolicitudFieldErrors = useMemo(() => {
    const result: SolicitudFieldErrors = {};
    for (const field of CREATE_FIELD_ORDER) {
      const serverErr = serverFieldErrors[field];
      if (serverErr) {
        result[field] = serverErr;
        continue;
      }
      const shouldShowClientError =
        hasAttemptedSubmit ||
        Boolean(touchedFields[field]) ||
        (field === 'asunto' && values.asunto.length > 100);
      if (shouldShowClientError && validationErrors[field]) {
        result[field] = validationErrors[field];
      }
    }
    return result;
  }, [
    hasAttemptedSubmit,
    serverFieldErrors,
    touchedFields,
    validationErrors,
    values.asunto.length,
  ]);

  const invalidCount = useMemo(
    () => CREATE_FIELD_ORDER.filter((field) => Boolean(visibleErrors[field])).length,
    [visibleErrors],
  );

  const isDirty = useMemo(() => {
    return (
      values.generarFolioAutomatico !== initialDefaults.generarFolioAutomatico ||
      values.folio !== initialDefaults.folio ||
      values.fechaEntrega !== initialDefaults.fechaEntrega ||
      values.horaEntrega !== initialDefaults.horaEntrega ||
      values.areaReceptoraId !== initialDefaults.areaReceptoraId ||
      values.asunto !== initialDefaults.asunto ||
      values.medioEntrega !== initialDefaults.medioEntrega ||
      values.estatusEntrega !== initialDefaults.estatusEntrega ||
      values.tipoSolicitud !== initialDefaults.tipoSolicitud ||
      values.tipoEvidencia !== initialDefaults.tipoEvidencia ||
      values.tieneAcuse !== initialDefaults.tieneAcuse ||
      values.tipoAcuse !== initialDefaults.tipoAcuse ||
      values.fechaVencimiento !== initialDefaults.fechaVencimiento
    );
  }, [initialDefaults, values]);

  const isFormLocked = submitStatus === 'submitting' || submitStatus === 'uncertain';
  const isUncertain = submitStatus === 'uncertain';

  useEffect(() => {
    const shouldWarn = (isDirty || isUncertain) && submitStatus !== 'succeeded';
    if (!shouldWarn) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (allowExitRef.current) return;
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty, isUncertain, submitStatus]);

  const registerFieldRef = useCallback(
    (field: SolicitudFieldName) => (element: HTMLElement | null) => {
      fieldRefs.current[field] = element;
    },
    [],
  );

  const focusField = useCallback((field: SolicitudFieldName) => {
    setFocusRequest((prev) => ({ field, seq: (prev?.seq ?? 0) + 1 }));
  }, []);

  useEffect(() => {
    if (!focusRequest || isFormLocked) return;
    fieldRefs.current[focusRequest.field]?.focus();
  }, [focusRequest, isFormLocked]);

  const clearServerFieldError = useCallback((field: SolicitudFieldName) => {
    setServerBannerError(null);
    setServerFieldErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }, []);

  const updateField = useCallback(
    <K extends keyof SolicitudFormValues>(field: K, value: SolicitudFormValues[K]) => {
      if (isFormLocked) return;
      setFormState((prev) => ({
        ...prev,
        values: { ...prev.values, [field]: value },
      }));
      if (field !== 'generarFolioAutomatico') {
        clearServerFieldError(field as SolicitudFieldName);
      }
    },
    [clearServerFieldError, isFormLocked],
  );

  const handleFieldBlur = useCallback((field: SolicitudFieldName) => {
    setTouchedFields((prev) => (prev[field] ? prev : { ...prev, [field]: true }));
  }, []);

  const toggleFolioAutomatico = useCallback(() => {
    if (isFormLocked) return;
    clearServerFieldError('folio');
    setFormState((prev) => {
      if (!prev.values.generarFolioAutomatico) {
        return {
          manualFolioDraft: prev.values.folio,
          values: { ...prev.values, generarFolioAutomatico: true },
        };
      }
      return {
        manualFolioDraft: prev.manualFolioDraft,
        values: {
          ...prev.values,
          generarFolioAutomatico: false,
          folio: prev.manualFolioDraft,
        },
      };
    });
  }, [clearServerFieldError, isFormLocked]);

  const handleTieneAcuseChange = useCallback(
    (nextValue: '' | 'si' | 'no') => {
      if (isFormLocked) return;
      clearServerFieldError('tieneAcuse');
      clearServerFieldError('tipoAcuse');
      setFormState((prev) => ({
        ...prev,
        values: applyTieneAcuseChange(prev.values, nextValue),
      }));
    },
    [clearServerFieldError, isFormLocked],
  );

  const buildPayload = useCallback((): CrearSolicitudPayload | null => {
    const editablePayload = buildActualizarSolicitudPayload(values);
    if (!editablePayload) return null;

    return {
      generarFolioAutomatico: values.generarFolioAutomatico,
      folio: values.generarFolioAutomatico ? null : values.folio.trim(),
      ...editablePayload,
    };
  }, [values]);

  const executeSubmission = useCallback(
    async (record: PendingSubmissionRecord) => {
      pendingSubmissionRef.current = record;
      setSubmitStatus('submitting');
      setModalState('none');
      setServerBannerError(null);

      try {
        const resultado = await crearSolicitud(record.payload, record.idempotencyKey);
        clearUncertainSubmission();
        pendingSubmissionRef.current = null;
        setConfirmacion(
          buildSolicitudConfirmacionResumen(resultado, record.values, catalogosQuery.data),
        );
        setSubmitStatus('succeeded');
      } catch (error) {
        if (error instanceof HttpClientError) {
          if (error.statusCode === 409) {
            clearUncertainSubmission();
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            const code = getErrorCode(error.details);
            if (code === 'Solicitud.FolioDuplicado') {
              setServerFieldErrors({
                folio: 'El folio ingresado ya está registrado. Introduce uno diferente',
              });
              focusField('folio');
            } else if (code === 'Solicitud.IdempotenciaConflicto') {
              setServerBannerError(
                'Se detectó un conflicto de idempotencia en el envío. Verifica los datos e inténtalo nuevamente.',
              );
            } else {
              setServerBannerError(
                'No fue posible completar el registro por un conflicto con los datos ingresados.',
              );
            }
            return;
          }

          if (error.statusCode === 400) {
            clearCsrfToken();
            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'El token de seguridad expiró; se renovará al pulsar Reintentar conservando la misma operación.',
              );
              return;
            }
            clearUncertainSubmission();
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            const mapped = mapValidationProblemDetails(error.details, CREATE_SERVER_FIELD_MAP);
            if (Object.keys(mapped).length > 0) {
              setServerFieldErrors(mapped);
              const firstServerField = CREATE_FIELD_ORDER.find((field) => Boolean(mapped[field]));
              if (firstServerField) focusField(firstServerField);
            } else {
              setServerBannerError(
                'La solicitud no pudo validarse o la protección de seguridad caducó. Revisa los datos e inténtalo de nuevo.',
              );
            }
            return;
          }

          if (error.statusCode === 401) {
            clearCsrfToken();
            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'Tu sesión expiró. Inicia sesión con la misma cuenta para recuperar esta operación.',
              );
              void queryClient.invalidateQueries({ queryKey: sessionKey });
              return;
            }
            clearUncertainSubmission();
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError('Tu sesión expiró. Inicia sesión nuevamente para continuar.');
            void queryClient.invalidateQueries({ queryKey: sessionKey });
            return;
          }

          if (error.statusCode === 403) {
            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'No tienes permisos para completar la recuperación; la operación pendiente se conservó.',
              );
              return;
            }
            clearUncertainSubmission();
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError('No tienes permisos para registrar solicitudes.');
            return;
          }
        }

        // Fallo de red, 500, 503 u otro resultado incierto: conservar payload, valores e Idempotency-Key
        const uncertainRecord: PendingSubmissionRecord = {
          ...record,
          wasUncertain: true,
        };
        pendingSubmissionRef.current = uncertainRecord;
        saveUncertainSubmission(uncertainRecord);
        setSubmitStatus('uncertain');
      }
    },
    [catalogosQuery.data, focusField, queryClient],
  );

  const requestSubmit = useCallback(() => {
    if (isFormLocked || catalogosQuery.isPending || catalogosQuery.isError) return;

    setHasAttemptedSubmit(true);
    setServerBannerError(null);
    setServerFieldErrors({});

    const currentErrors = validateSolicitudForm(values, catalogosQuery.data);
    const firstInvalid = CREATE_FIELD_ORDER.find((field) => Boolean(currentErrors[field]));

    if (firstInvalid) {
      focusField(firstInvalid);
      return;
    }

    setSubmitStatus('confirming');
    setModalState('confirm-register');
  }, [
    catalogosQuery.data,
    catalogosQuery.isError,
    catalogosQuery.isPending,
    focusField,
    isFormLocked,
    values,
  ]);

  const confirmRegister = useCallback(() => {
    if (submitStatus === 'submitting' || submitStatus === 'uncertain') return;
    const payload = buildPayload();
    if (!payload) {
      setModalState('none');
      setSubmitStatus('idle');
      return;
    }

    const record: PendingSubmissionRecord = {
      ownerUserId: userId,
      idempotencyKey: crypto.randomUUID(),
      payload,
      values: formState.values,
      manualFolioDraft: formState.manualFolioDraft,
      wasUncertain: false,
    };
    void executeSubmission(record);
  }, [
    buildPayload,
    executeSubmission,
    formState.manualFolioDraft,
    formState.values,
    submitStatus,
    userId,
  ]);

  const retryUncertainSubmission = useCallback(() => {
    if (submitStatus === 'submitting') return;

    const currentRecord = pendingSubmissionRef.current;
    const storedRecord = readUncertainSubmission();
    const record = currentRecord ?? (storedRecord?.ownerUserId === userId ? storedRecord : null);

    if (
      !record ||
      record.ownerUserId !== userId ||
      (storedRecord && storedRecord.ownerUserId !== userId)
    ) {
      clearUncertainSubmission();
      pendingSubmissionRef.current = null;
      setSubmitStatus('idle');
      return;
    }

    void executeSubmission({ ...record, wasUncertain: true });
  }, [executeSubmission, submitStatus, userId]);

  const cancelConfirmRegister = useCallback(() => {
    if (submitStatus === 'submitting') return;
    setModalState('none');
    setSubmitStatus('idle');
  }, [submitStatus]);

  const requestCancel = useCallback(() => {
    if (submitStatus === 'submitting') return;
    if (submitStatus === 'uncertain') {
      setModalState('confirm-uncertain-exit');
      return;
    }
    setModalState('confirm-discard');
  }, [submitStatus]);

  const requestExitToDashboard = useCallback(() => {
    if (submitStatus === 'submitting') return;
    if (submitStatus === 'uncertain') {
      setModalState('confirm-uncertain-exit');
      return;
    }
    if (isDirty && submitStatus !== 'succeeded') {
      setModalState('confirm-discard');
      return;
    }
    allowExitRef.current = true;
    navigate('/admin/dashboard');
  }, [isDirty, navigate, submitStatus]);

  const closeDiscardModal = useCallback(() => {
    setModalState('none');
  }, []);

  const confirmExitToDashboard = useCallback(() => {
    allowExitRef.current = true;
    setModalState('none');
    navigate('/admin/dashboard');
  }, [navigate]);

  const irAEditarSolicitud = useCallback(() => {
    const folio = confirmacion?.resultado.folio;
    if (!folio) return;
    allowExitRef.current = true;
    setModalState('none');
    navigate(`/admin/solicitudes/${encodeURIComponent(folio)}/editar`);
  }, [confirmacion?.resultado.folio, navigate]);

  const resetFormulario = useCallback(() => {
    const freshDefaults = createInitialFormValues();
    clearUncertainSubmission();
    pendingSubmissionRef.current = null;
    allowExitRef.current = false;
    setInitialDefaults(freshDefaults);
    setFormState({ values: freshDefaults, manualFolioDraft: '' });
    setTouchedFields({});
    setHasAttemptedSubmit(false);
    setServerFieldErrors({});
    setServerBannerError(null);
    setConfirmacion(null);
    setModalState('none');
    setSubmitStatus('idle');
  }, []);

  return {
    catalogosQuery,
    selectableEstatusEntrega,
    selectableTiposAcuse,
    values,
    folioDisplayValue: values.generarFolioAutomatico ? 'Se asignará al guardar' : values.folio,
    diasRestantes,
    diasRestantesTone,
    visibleErrors,
    invalidCount,
    showValidationSummary: hasAttemptedSubmit && invalidCount > 0,
    serverBannerError,
    submitStatus,
    isFormLocked,
    isUncertain,
    modalState,
    confirmacion,
    registerFieldRef,
    updateField,
    handleFieldBlur,
    toggleFolioAutomatico,
    handleTieneAcuseChange,
    requestSubmit,
    confirmRegister,
    retryUncertainSubmission,
    cancelConfirmRegister,
    requestCancel,
    requestExitToDashboard,
    closeDiscardModal,
    confirmExitToDashboard,
    irAEditarSolicitud,
    resetFormulario,
  };
}
