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
  SubmitStatus,
} from '../types/solicitudes.types';
import {
  compareCalendarDates,
  computeDiasRestantesMexicoCity,
  getDiasRestantesTone,
  getMexicoCityNowParts,
  isValidTime24h,
  parseDdMmYyyy,
  toIsoDateString,
} from './solicitudesDateUtils';

const FOLIO_POLICY_REGEX = /^(?=.*[A-Za-z0-9])[A-Za-z0-9._/-]{1,100}$/;

const FIELD_ORDER: readonly SolicitudFieldName[] = [
  'folio',
  'fechaEntrega',
  'horaEntrega',
  'areaReceptoraId',
  'asunto',
  'medioEntrega',
  'estatusEntrega',
  'tipoSolicitud',
  'tipoEvidencia',
  'tieneAcuse',
  'tipoAcuse',
  'fechaVencimiento',
];

const SERVER_FIELD_MAP: Record<string, SolicitudFieldName> = {
  folio: 'folio',
  fechaentrega: 'fechaEntrega',
  horaentrega: 'horaEntrega',
  areareceptoraid: 'areaReceptoraId',
  asunto: 'asunto',
  medioentrega: 'medioEntrega',
  estatusentrega: 'estatusEntrega',
  tiposolicitud: 'tipoSolicitud',
  tipoevidencia: 'tipoEvidencia',
  tieneacuse: 'tieneAcuse',
  tipoacuse: 'tipoAcuse',
  fechavencimiento: 'fechaVencimiento',
};

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

export function validateSolicitudForm(values: SolicitudFormValues): SolicitudFieldErrors {
  const errors: SolicitudFieldErrors = {};

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

  const parsedEntrega = parseDdMmYyyy(values.fechaEntrega);
  if (!values.fechaEntrega.trim()) {
    errors.fechaEntrega = 'Ingresa la fecha de entrega.';
  } else if (!parsedEntrega) {
    errors.fechaEntrega = 'Ingresa una fecha válida con formato dd/mm/aaaa.';
  }

  if (!values.horaEntrega.trim()) {
    errors.horaEntrega = 'Ingresa la hora de entrega.';
  } else if (!isValidTime24h(values.horaEntrega)) {
    errors.horaEntrega = 'Ingresa una hora válida en formato 24 h (HH:mm).';
  }

  if (!values.areaReceptoraId) {
    errors.areaReceptoraId = 'Selecciona el área o dependencia receptora.';
  }

  if (!values.asunto.trim()) {
    errors.asunto = 'Ingresa el asunto corto de la solicitud.';
  } else if (values.asunto.length > 100) {
    errors.asunto = 'El asunto corto no puede exceder 100 caracteres.';
  }

  if (!values.medioEntrega) {
    errors.medioEntrega = 'Selecciona el medio de entrega.';
  }

  if (!values.estatusEntrega || values.estatusEntrega === 'Vencida') {
    errors.estatusEntrega = 'Selecciona el estatus de entrega.';
  }

  if (!values.tipoSolicitud) {
    errors.tipoSolicitud = 'Selecciona el tipo de solicitud.';
  }

  if (!values.tipoEvidencia) {
    errors.tipoEvidencia = 'Selecciona el tipo de evidencia.';
  }

  if (values.tieneAcuse !== 'si' && values.tieneAcuse !== 'no') {
    errors.tieneAcuse = 'Indica si cuenta con acuse.';
  }

  if (values.tieneAcuse === 'si' && (!values.tipoAcuse || values.tipoAcuse === 'NoAplica')) {
    errors.tipoAcuse = 'Selecciona un tipo de acuse válido.';
  }

  const parsedVencimiento = parseDdMmYyyy(values.fechaVencimiento);
  if (!values.fechaVencimiento.trim()) {
    errors.fechaVencimiento = 'Ingresa el plazo de vencimiento.';
  } else if (!parsedVencimiento) {
    errors.fechaVencimiento = 'Ingresa una fecha válida con formato dd/mm/aaaa.';
  } else if (parsedEntrega && compareCalendarDates(parsedVencimiento, parsedEntrega) < 0) {
    errors.fechaVencimiento = 'El plazo de vencimiento no puede ser anterior a la fecha de entrega.';
  }

  return errors;
}

function mapValidationProblemDetails(details: unknown): SolicitudFieldErrors {
  if (!details || typeof details !== 'object') return {};
  const maybeErrors = (details as { errors?: unknown }).errors;
  if (!maybeErrors || typeof maybeErrors !== 'object') return {};

  const mapped: SolicitudFieldErrors = {};
  for (const [rawKey, rawMessages] of Object.entries(maybeErrors as Record<string, unknown>)) {
    const normalized = rawKey.trim().replace(/^\$\./, '').toLowerCase();
    const fieldName = SERVER_FIELD_MAP[normalized];
    if (!fieldName) continue;
    if (Array.isArray(rawMessages) && typeof rawMessages[0] === 'string' && rawMessages[0].trim()) {
      mapped[fieldName] = rawMessages[0];
    }
  }
  return mapped;
}

function getErrorCode(details: unknown): string | undefined {
  if (!details || typeof details !== 'object') return undefined;
  const code = (details as { code?: unknown }).code;
  return typeof code === 'string' ? code : undefined;
}

export function useNuevaSolicitudViewModel() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [initialDefaults, setInitialDefaults] = useState<SolicitudFormValues>(() =>
    createInitialFormValues(),
  );
  const [formState, setFormState] = useState<{
    values: SolicitudFormValues;
    manualFolioDraft: string;
  }>(() => ({
    values: initialDefaults,
    manualFolioDraft: '',
  }));
  const { values } = formState;
  const [touchedFields, setTouchedFields] = useState<Partial<Record<SolicitudFieldName, boolean>>>(
    {},
  );
  const [hasAttemptedSubmit, setHasAttemptedSubmit] = useState(false);
  const [serverFieldErrors, setServerFieldErrors] = useState<SolicitudFieldErrors>({});
  const [serverBannerError, setServerBannerError] = useState<string | null>(null);
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>('idle');
  const [modalState, setModalState] = useState<SolicitudModalState>('none');
  const [confirmacion, setConfirmacion] = useState<SolicitudConfirmacionResumen | null>(null);

  const pendingSubmissionRef = useRef<{
    idempotencyKey: string;
    payload: CrearSolicitudPayload;
  } | null>(null);
  const allowExitRef = useRef(false);
  const fieldRefs = useRef<Partial<Record<SolicitudFieldName, HTMLElement | null>>>({});

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
    () => (catalogosQuery.data?.estatusEntrega ?? []).filter((item) => item.codigo !== 'Vencida'),
    [catalogosQuery.data?.estatusEntrega],
  );

  const selectableTiposAcuse = useMemo(() => {
    const raw = catalogosQuery.data?.tiposAcuse ?? [];
    if (values.tieneAcuse === 'no') {
      const hasNoAplica = raw.some((item) => item.codigo === 'NoAplica');
      return hasNoAplica ? raw : [...raw, { codigo: 'NoAplica', nombre: 'No aplica' }];
    }
    if (values.tieneAcuse === 'si') {
      return raw.filter((item) => item.codigo !== 'NoAplica');
    }
    return [];
  }, [catalogosQuery.data?.tiposAcuse, values.tieneAcuse]);

  const diasRestantes = useMemo(
    () => computeDiasRestantesMexicoCity(values.fechaVencimiento),
    [values.fechaVencimiento],
  );

  const diasRestantesTone = useMemo(() => getDiasRestantesTone(diasRestantes), [diasRestantes]);

  const validationErrors = useMemo(() => validateSolicitudForm(values), [values]);

  const visibleErrors: SolicitudFieldErrors = useMemo(() => {
    const result: SolicitudFieldErrors = {};
    for (const field of FIELD_ORDER) {
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
    () => FIELD_ORDER.filter((field) => Boolean(visibleErrors[field])).length,
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
    requestAnimationFrame(() => {
      fieldRefs.current[field]?.focus();
    });
  }, []);

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
      setFormState((prev) => {
        if (nextValue === 'no') {
          return {
            ...prev,
            values: { ...prev.values, tieneAcuse: 'no', tipoAcuse: 'NoAplica' },
          };
        }
        if (nextValue === 'si') {
          return {
            ...prev,
            values: {
              ...prev.values,
              tieneAcuse: 'si',
              tipoAcuse: prev.values.tipoAcuse === 'NoAplica' ? '' : prev.values.tipoAcuse,
            },
          };
        }
        return {
          ...prev,
          values: { ...prev.values, tieneAcuse: '', tipoAcuse: '' },
        };
      });
    },
    [clearServerFieldError, isFormLocked],
  );

  const buildPayload = useCallback((): CrearSolicitudPayload | null => {
    const parsedEntrega = parseDdMmYyyy(values.fechaEntrega);
    const parsedVencimiento = parseDdMmYyyy(values.fechaVencimiento);
    if (!parsedEntrega || !parsedVencimiento) return null;

    return {
      generarFolioAutomatico: values.generarFolioAutomatico,
      folio: values.generarFolioAutomatico ? null : values.folio.trim(),
      fechaEntrega: toIsoDateString(parsedEntrega),
      horaEntrega: values.horaEntrega.trim(),
      areaReceptoraId: values.areaReceptoraId,
      asunto: values.asunto.trim(),
      medioEntrega: values.medioEntrega,
      estatusEntrega: values.estatusEntrega,
      tipoSolicitud: values.tipoSolicitud,
      tipoEvidencia: values.tipoEvidencia,
      tieneAcuse: values.tieneAcuse === 'si',
      tipoAcuse: values.tieneAcuse === 'no' ? 'NoAplica' : values.tipoAcuse,
      fechaVencimiento: toIsoDateString(parsedVencimiento),
    };
  }, [values]);

  const buildConfirmacionResumen = useCallback(
    (resultado: SolicitudConfirmacionResumen['resultado']): SolicitudConfirmacionResumen => {
      const catalogos = catalogosQuery.data;
      const areaNombre =
        catalogos?.areasReceptoras.find((a) => a.id === values.areaReceptoraId)?.nombre ??
        values.areaReceptoraId;
      const medioNombre =
        catalogos?.mediosEntrega.find((m) => m.codigo === values.medioEntrega)?.nombre ??
        values.medioEntrega;
      const tipoNombre =
        catalogos?.tiposSolicitud.find((t) => t.codigo === values.tipoSolicitud)?.nombre ??
        values.tipoSolicitud;
      const evidenciaNombre =
        catalogos?.tiposEvidencia.find((e) => e.codigo === values.tipoEvidencia)?.nombre ??
        values.tipoEvidencia;
      const tipoAcuseNombre =
        values.tieneAcuse === 'no'
          ? 'No aplica'
          : (catalogos?.tiposAcuse.find((a) => a.codigo === values.tipoAcuse)?.nombre ??
            values.tipoAcuse);

      return {
        resultado,
        areaReceptoraNombre: areaNombre,
        asunto: values.asunto.trim(),
        fechaEntrega: values.fechaEntrega.trim(),
        horaEntrega: values.horaEntrega.trim(),
        medioEntregaNombre: medioNombre,
        tipoSolicitudNombre: tipoNombre,
        tipoEvidenciaNombre: evidenciaNombre,
        tieneAcuseTexto: values.tieneAcuse === 'si' ? 'Sí' : 'No',
        tipoAcuseNombre,
      };
    },
    [catalogosQuery.data, values],
  );

  const executeSubmission = useCallback(
    async (idempotencyKey: string, payload: CrearSolicitudPayload) => {
      setSubmitStatus('submitting');
      setModalState('none');
      setServerBannerError(null);

      try {
        const resultado = await crearSolicitud(payload, idempotencyKey);
        pendingSubmissionRef.current = null;
        setConfirmacion(buildConfirmacionResumen(resultado));
        setSubmitStatus('succeeded');
      } catch (error) {
        if (error instanceof HttpClientError) {
          if (error.statusCode === 400) {
            clearCsrfToken();
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            const mapped = mapValidationProblemDetails(error.details);
            if (Object.keys(mapped).length > 0) {
              setServerFieldErrors(mapped);
              const firstServerField = FIELD_ORDER.find((field) => Boolean(mapped[field]));
              if (firstServerField) focusField(firstServerField);
            } else {
              setServerBannerError(
                'La solicitud no pudo validarse o la protección de seguridad caducó. Revisa los datos e inténtalo de nuevo.',
              );
            }
            return;
          }

          if (error.statusCode === 409) {
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

          if (error.statusCode === 401 || error.statusCode === 403) {
            if (error.statusCode === 401) {
              clearCsrfToken();
              void queryClient.invalidateQueries({ queryKey: sessionKey });
            }
            pendingSubmissionRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError(
              error.statusCode === 401
                ? 'Tu sesión expiró. Inicia sesión nuevamente para continuar.'
                : 'No tienes permisos para registrar solicitudes.',
            );
            return;
          }
        }

        // Fallo de red, 500, 503 u otro resultado incierto: conservar payload e Idempotency-Key
        setSubmitStatus('uncertain');
      }
    },
    [buildConfirmacionResumen, focusField],
  );

  const requestSubmit = useCallback(() => {
    if (isFormLocked || catalogosQuery.isPending || catalogosQuery.isError) return;

    setHasAttemptedSubmit(true);
    setServerBannerError(null);
    setServerFieldErrors({});

    const currentErrors = validateSolicitudForm(values);
    const firstInvalid = FIELD_ORDER.find((field) => Boolean(currentErrors[field]));

    if (firstInvalid) {
      focusField(firstInvalid);
      return;
    }

    setSubmitStatus('confirming');
    setModalState('confirm-register');
  }, [catalogosQuery.isError, catalogosQuery.isPending, focusField, isFormLocked, values]);

  const confirmRegister = useCallback(() => {
    if (submitStatus === 'submitting') return;
    const payload = buildPayload();
    if (!payload) {
      setModalState('none');
      setSubmitStatus('idle');
      return;
    }

    const idempotencyKey = crypto.randomUUID();
    pendingSubmissionRef.current = { idempotencyKey, payload };
    void executeSubmission(idempotencyKey, payload);
  }, [buildPayload, executeSubmission, submitStatus]);

  const retryUncertainSubmission = useCallback(() => {
    if (submitStatus === 'submitting' || !pendingSubmissionRef.current) return;
    const { idempotencyKey, payload } = pendingSubmissionRef.current;
    void executeSubmission(idempotencyKey, payload);
  }, [executeSubmission, submitStatus]);

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

  const resetFormulario = useCallback(() => {
    const freshDefaults = createInitialFormValues();
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
    resetFormulario,
  };
}
