import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { HttpClientError } from '../../../core/api/httpClient';
import { clearCsrfToken } from '../../auth/api/authApi';
import { sessionKey } from '../../auth/hooks/useSessionViewModel';
import {
  actualizarSolicitud,
  fetchSolicitudesCatalogos,
  obtenerSolicitudParaEdicion,
} from '../api/solicitudesApi';
import type {
  ActualizarSolicitudPayload,
  EditarSolicitudModalState,
  SolicitudConfirmacionResumen,
  SolicitudEdicionResponse,
  SolicitudEditableFieldErrors,
  SolicitudEditableFieldName,
  SolicitudEditableFormValues,
  SolicitudToastMessage,
  SubmitStatus,
} from '../types/solicitudes.types';
import {
  computeDiasRestantesMexicoCity,
  getDiasRestantesTone,
} from './solicitudesDateUtils';
import {
  EDITABLE_FIELD_ORDER,
  EDITABLE_SERVER_FIELD_MAP,
  applyTieneAcuseChange,
  areEditableSolicitudValuesEquivalent,
  buildActualizarSolicitudPayload,
  buildSolicitudConfirmacionResumen,
  getSelectableEstatusEntrega,
  getSelectableTiposAcuse,
  mapSolicitudEdicionToFormValues,
  mapValidationProblemDetails,
  validateEditableSolicitudFields,
} from './solicitudesFormUtils';

const EMPTY_EDITABLE_VALUES: SolicitudEditableFormValues = {
  fechaEntrega: '',
  horaEntrega: '',
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

interface PendingUpdateRecord {
  id: string;
  folio: string;
  etag: string;
  payload: ActualizarSolicitudPayload;
  values: SolicitudEditableFormValues;
  wasUncertain: boolean;
}

export type InitialLoadErrorType = 'none' | 'b04' | 'unauthorized' | 'forbidden';

export function useEditarSolicitudViewModel(routeFolio: string) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [activeRouteFolio, setActiveRouteFolio] = useState(routeFolio);
  const [isHydrated, setIsHydrated] = useState(false);
  const [loadedSolicitud, setLoadedSolicitud] = useState<SolicitudEdicionResponse | null>(null);
  const [currentEtag, setCurrentEtag] = useState('');
  const [baselineValues, setBaselineValues] = useState<SolicitudEditableFormValues | null>(null);
  const [values, setValues] = useState<SolicitudEditableFormValues>(EMPTY_EDITABLE_VALUES);

  const [touchedFields, setTouchedFields] = useState<
    Partial<Record<SolicitudEditableFieldName, boolean>>
  >({});
  const [hasAttemptedSubmit, setHasAttemptedSubmit] = useState(false);
  const [serverFieldErrors, setServerFieldErrors] = useState<SolicitudEditableFieldErrors>({});
  const [serverBannerError, setServerBannerError] = useState<string | null>(null);
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>('idle');
  const [modalState, setModalState] = useState<EditarSolicitudModalState>('none');
  const [confirmacion, setConfirmacion] = useState<SolicitudConfirmacionResumen | null>(null);

  const [isConcurrencyStale, setIsConcurrencyStale] = useState(false);
  const [isReloadingDatos, setIsReloadingDatos] = useState(false);
  const [reloadError, setReloadError] = useState<string | null>(null);
  const [toast, setToast] = useState<SolicitudToastMessage | null>(null);
  const [focusRequest, setFocusRequest] = useState<{
    field: SolicitudEditableFieldName;
    seq: number;
  } | null>(null);

  if (activeRouteFolio !== routeFolio) {
    setActiveRouteFolio(routeFolio);
    setIsHydrated(false);
    setLoadedSolicitud(null);
    setCurrentEtag('');
    setBaselineValues(null);
    setValues(EMPTY_EDITABLE_VALUES);
    setTouchedFields({});
    setHasAttemptedSubmit(false);
    setServerFieldErrors({});
    setServerBannerError(null);
    setSubmitStatus('idle');
    setModalState('none');
    setConfirmacion(null);
    setIsConcurrencyStale(false);
    setIsReloadingDatos(false);
    setReloadError(null);
  }

  const pendingUpdateRef = useRef<PendingUpdateRecord | null>(null);
  const isSubmittingRef = useRef(false);
  const reloadSeqRef = useRef(0);
  const allowExitRef = useRef(false);
  const pendingExitDestinationRef = useRef<'/admin/dashboard' | '/admin/solicitudes'>(
    '/admin/dashboard',
  );
  const fieldRefs = useRef<Partial<Record<SolicitudEditableFieldName, HTMLElement | null>>>({});
  const toastTimerRef = useRef<number | null>(null);
  const toastSeqRef = useRef(0);

  useEffect(() => {
    return () => {
      if (toastTimerRef.current !== null) {
        window.clearTimeout(toastTimerRef.current);
      }
    };
  }, []);

  const dismissToast = useCallback(() => {
    if (toastTimerRef.current !== null) {
      window.clearTimeout(toastTimerRef.current);
      toastTimerRef.current = null;
    }
    setToast(null);
  }, []);

  const showToast = useCallback((tone: 'info' | 'success', text: string) => {
    if (toastTimerRef.current !== null) {
      window.clearTimeout(toastTimerRef.current);
    }
    toastSeqRef.current += 1;
    const id = toastSeqRef.current;
    setToast({ id, tone, text });
    toastTimerRef.current = window.setTimeout(() => {
      setToast((current) => (current?.id === id ? null : current));
      toastTimerRef.current = null;
    }, 6000);
  }, []);

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
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  const edicionQuery = useQuery({
    queryKey: ['solicitudes', 'edicion', routeFolio],
    queryFn: async () => {
      try {
        return await obtenerSolicitudParaEdicion(routeFolio);
      } catch (error) {
        if (error instanceof HttpClientError && error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
        }
        throw error;
      }
    },
    retry: false,
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  // Hidrata exclusivamente cuando la consulta del montaje actual y los catálogos resolvieron sin error.
  useEffect(() => {
    if (
      isHydrated ||
      edicionQuery.isFetching ||
      edicionQuery.isError ||
      !edicionQuery.isFetchedAfterMount ||
      !edicionQuery.data ||
      catalogosQuery.isFetching ||
      catalogosQuery.isError ||
      !catalogosQuery.data
    ) {
      return;
    }

    const mapped = mapSolicitudEdicionToFormValues(edicionQuery.data.solicitud);
    setLoadedSolicitud(edicionQuery.data.solicitud);
    setCurrentEtag(edicionQuery.data.etag);
    setBaselineValues(mapped);
    setValues(mapped);
    setIsHydrated(true);
  }, [
    catalogosQuery.data,
    catalogosQuery.isError,
    catalogosQuery.isFetching,
    edicionQuery.data,
    edicionQuery.isError,
    edicionQuery.isFetchedAfterMount,
    edicionQuery.isFetching,
    isHydrated,
  ]);

  const isInitialNotFound = useMemo(() => {
    if (
      isHydrated ||
      edicionQuery.isFetching ||
      !edicionQuery.isFetchedAfterMount ||
      !edicionQuery.isError
    ) {
      return false;
    }
    return (
      edicionQuery.error instanceof HttpClientError &&
      (edicionQuery.error.statusCode === 404 || edicionQuery.error.statusCode === 410)
    );
  }, [
    edicionQuery.error,
    edicionQuery.isError,
    edicionQuery.isFetchedAfterMount,
    edicionQuery.isFetching,
    isHydrated,
  ]);

  const initialLoadErrorType: InitialLoadErrorType = useMemo(() => {
    if (
      isHydrated ||
      isInitialNotFound ||
      edicionQuery.isFetching ||
      !edicionQuery.isFetchedAfterMount ||
      catalogosQuery.isFetching
    ) {
      return 'none';
    }
    const activeError = edicionQuery.isError
      ? edicionQuery.error
      : catalogosQuery.isError
        ? catalogosQuery.error
        : null;
    if (!activeError) return 'none';

    if (activeError instanceof HttpClientError) {
      if (activeError.statusCode === 401) return 'unauthorized';
      if (activeError.statusCode === 403) return 'forbidden';
    }
    return 'b04';
  }, [
    catalogosQuery.error,
    catalogosQuery.isError,
    catalogosQuery.isFetching,
    edicionQuery.error,
    edicionQuery.isError,
    edicionQuery.isFetchedAfterMount,
    edicionQuery.isFetching,
    isHydrated,
    isInitialNotFound,
  ]);

  const isPreloading =
    !isHydrated && !isInitialNotFound && initialLoadErrorType === 'none';

  const isNotFoundModalOpen = isInitialNotFound || modalState === 'not-found';

  const folioReal = loadedSolicitud?.folio ?? routeFolio;

  const selectableEstatusEntrega = useMemo(
    () => getSelectableEstatusEntrega(catalogosQuery.data),
    [catalogosQuery.data],
  );

  const selectableTiposAcuse = useMemo(
    () => getSelectableTiposAcuse(catalogosQuery.data, values.tieneAcuse),
    [catalogosQuery.data, values.tieneAcuse],
  );

  const diasRestantes = useMemo(() => {
    if (!isHydrated) return null;
    if (
      loadedSolicitud &&
      baselineValues &&
      values.fechaVencimiento.trim() === baselineValues.fechaVencimiento.trim()
    ) {
      return loadedSolicitud.diasRestantes;
    }
    return computeDiasRestantesMexicoCity(values.fechaVencimiento);
  }, [baselineValues, isHydrated, loadedSolicitud, values.fechaVencimiento]);

  const diasRestantesTone = useMemo(() => getDiasRestantesTone(diasRestantes), [diasRestantes]);

  const validationErrors = useMemo(
    () => validateEditableSolicitudFields(values, catalogosQuery.data),
    [catalogosQuery.data, values],
  );

  const visibleErrors: SolicitudEditableFieldErrors = useMemo(() => {
    if (!isHydrated) return {};
    const result: SolicitudEditableFieldErrors = {};
    for (const field of EDITABLE_FIELD_ORDER) {
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
    isHydrated,
    serverFieldErrors,
    touchedFields,
    validationErrors,
    values.asunto.length,
  ]);

  const invalidCount = useMemo(
    () => EDITABLE_FIELD_ORDER.filter((field) => Boolean(visibleErrors[field])).length,
    [visibleErrors],
  );

  const isDirty = useMemo(() => {
    if (!isHydrated || !baselineValues) return false;
    return !areEditableSolicitudValuesEquivalent(values, baselineValues);
  }, [baselineValues, isHydrated, values]);

  const isFormLocked =
    submitStatus === 'submitting' || submitStatus === 'uncertain' || isReloadingDatos;
  const isUncertain = submitStatus === 'uncertain';
  const isSaveDisabled =
    !isHydrated ||
    !loadedSolicitud ||
    !currentEtag ||
    !catalogosQuery.data ||
    isFormLocked ||
    isConcurrencyStale;

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
    (field: SolicitudEditableFieldName) => (element: HTMLElement | null) => {
      fieldRefs.current[field] = element;
    },
    [],
  );

  const focusField = useCallback((field: SolicitudEditableFieldName) => {
    setFocusRequest((prev) => ({ field, seq: (prev?.seq ?? 0) + 1 }));
  }, []);

  useEffect(() => {
    if (!focusRequest || isFormLocked || !isHydrated) return;
    fieldRefs.current[focusRequest.field]?.focus();
  }, [focusRequest, isFormLocked, isHydrated]);

  const clearServerFieldError = useCallback((field: SolicitudEditableFieldName) => {
    setServerBannerError(null);
    setServerFieldErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }, []);

  const updateField = useCallback(
    <K extends keyof SolicitudEditableFormValues>(
      field: K,
      value: SolicitudEditableFormValues[K],
    ) => {
      if (isFormLocked || !isHydrated) return;
      setValues((prev) => ({ ...prev, [field]: value }));
      clearServerFieldError(field);
      setToast((current) =>
        current?.text === 'No hay cambios por guardar.' ? null : current,
      );
    },
    [clearServerFieldError, isFormLocked, isHydrated],
  );

  const handleFieldBlur = useCallback((field: SolicitudEditableFieldName) => {
    setTouchedFields((prev) => (prev[field] ? prev : { ...prev, [field]: true }));
  }, []);

  const handleTieneAcuseChange = useCallback(
    (nextValue: '' | 'si' | 'no') => {
      if (isFormLocked || !isHydrated) return;
      clearServerFieldError('tieneAcuse');
      clearServerFieldError('tipoAcuse');
      setValues((prev) => applyTieneAcuseChange(prev, nextValue));
      setToast((current) =>
        current?.text === 'No hay cambios por guardar.' ? null : current,
      );
    },
    [clearServerFieldError, isFormLocked, isHydrated],
  );

  const retryInitialLoad = useCallback(() => {
    if (edicionQuery.isError || !edicionQuery.data || !edicionQuery.isFetchedAfterMount) {
      void edicionQuery.refetch();
    }
    if (catalogosQuery.isError || !catalogosQuery.data) {
      void catalogosQuery.refetch();
    }
  }, [catalogosQuery, edicionQuery]);

  const executeUpdate = useCallback(
    async (record: PendingUpdateRecord) => {
      if (isSubmittingRef.current) return;
      isSubmittingRef.current = true;
      pendingUpdateRef.current = record;
      setSubmitStatus('submitting');
      setModalState('none');
      setServerBannerError(null);

      try {
        const { solicitud: updatedSolicitud, etag: nextEtag } = await actualizarSolicitud(
          record.id,
          record.payload,
          record.etag,
        );

        pendingUpdateRef.current = null;
        const mappedValues = mapSolicitudEdicionToFormValues(updatedSolicitud);

        setLoadedSolicitud(updatedSolicitud);
        setCurrentEtag(nextEtag);
        setBaselineValues(mappedValues);
        setValues(mappedValues);
        setTouchedFields({});
        setHasAttemptedSubmit(false);
        setServerFieldErrors({});
        setServerBannerError(null);
        setReloadError(null);
        setIsConcurrencyStale(!nextEtag);

        if (nextEtag) {
          const snapshot = { solicitud: updatedSolicitud, etag: nextEtag };
          queryClient.setQueryData(['solicitudes', 'edicion', updatedSolicitud.folio], snapshot);
          if (routeFolio !== updatedSolicitud.folio) {
            queryClient.setQueryData(['solicitudes', 'edicion', routeFolio], snapshot);
          }
        }

        const resumen = buildSolicitudConfirmacionResumen(
          {
            id: updatedSolicitud.id,
            folio: updatedSolicitud.folio,
            fechaVencimiento: updatedSolicitud.fechaVencimiento,
            diasRestantes: updatedSolicitud.diasRestantes,
            estatus: updatedSolicitud.estatusEfectivo,
          },
          mappedValues,
          catalogosQuery.data,
        );
        setConfirmacion(resumen);
        setSubmitStatus('succeeded');
        showToast('success', 'Cambios guardados. Vencimiento y días restantes actualizados.');
      } catch (error) {
        if (error instanceof HttpClientError) {
          if (error.statusCode === 404 || error.statusCode === 410) {
            pendingUpdateRef.current = null;
            setSubmitStatus('idle');
            setModalState('not-found');
            return;
          }

          if (error.statusCode === 409 || error.statusCode === 412) {
            pendingUpdateRef.current = null;
            setSubmitStatus('idle');
            setIsConcurrencyStale(true);
            setReloadError(null);
            setModalState('conflict-concurrency');
            return;
          }

          if (error.statusCode === 400) {
            clearCsrfToken();
            const mapped = mapValidationProblemDetails(error.details, EDITABLE_SERVER_FIELD_MAP);
            if (Object.keys(mapped).length > 0) {
              pendingUpdateRef.current = null;
              setSubmitStatus('idle');
              setServerFieldErrors(mapped);
              const firstServerField = EDITABLE_FIELD_ORDER.find((field) =>
                Boolean(mapped[field]),
              );
              if (firstServerField) focusField(firstServerField);
              return;
            }

            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'El token de seguridad expiró; se renovará al pulsar Reintentar conservando la misma operación.',
              );
              return;
            }

            pendingUpdateRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError(
              'La solicitud no pudo validarse o la protección de seguridad caducó. Revisa los datos e inténtalo de nuevo.',
            );
            return;
          }

          if (error.statusCode === 401) {
            clearCsrfToken();
            void queryClient.invalidateQueries({ queryKey: sessionKey });
            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'Tu sesión expiró. Inicia sesión nuevamente para continuar con esta operación.',
              );
              return;
            }
            pendingUpdateRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError('Tu sesión expiró. Inicia sesión nuevamente para continuar.');
            return;
          }

          if (error.statusCode === 403) {
            if (record.wasUncertain) {
              setSubmitStatus('uncertain');
              setServerBannerError(
                'No tienes permisos para completar esta operación; tus datos se conservaron.',
              );
              return;
            }
            pendingUpdateRef.current = null;
            setSubmitStatus('idle');
            setServerBannerError('No tienes permisos para editar esta solicitud.');
            return;
          }
        }

        // 500, 503 o fallo de red/timeout: conservar datos y exactamente el mismo ETag original (B-03)
        pendingUpdateRef.current = {
          ...record,
          wasUncertain: true,
        };
        setSubmitStatus('uncertain');
      } finally {
        isSubmittingRef.current = false;
      }
    },
    [catalogosQuery.data, focusField, queryClient, routeFolio, showToast],
  );

  const requestSubmit = useCallback(() => {
    if (isSaveDisabled) return;

    setHasAttemptedSubmit(true);
    setServerBannerError(null);
    setServerFieldErrors({});

    const currentErrors = validateEditableSolicitudFields(values, catalogosQuery.data);
    const firstInvalid = EDITABLE_FIELD_ORDER.find((field) => Boolean(currentErrors[field]));

    if (firstInvalid) {
      focusField(firstInvalid);
      return;
    }

    if (baselineValues && areEditableSolicitudValuesEquivalent(values, baselineValues)) {
      showToast('info', 'No hay cambios por guardar.');
      return;
    }

    dismissToast();
    setSubmitStatus('confirming');
    setModalState('confirm-save');
  }, [
    baselineValues,
    catalogosQuery.data,
    dismissToast,
    focusField,
    isSaveDisabled,
    showToast,
    values,
  ]);

  const confirmSave = useCallback(() => {
    if (
      isSubmittingRef.current ||
      submitStatus === 'submitting' ||
      submitStatus === 'uncertain' ||
      !loadedSolicitud ||
      !currentEtag
    ) {
      return;
    }

    const payload = buildActualizarSolicitudPayload(values);
    if (!payload) {
      setModalState('none');
      setSubmitStatus('idle');
      return;
    }

    const record: PendingUpdateRecord = {
      id: loadedSolicitud.id,
      folio: loadedSolicitud.folio,
      etag: currentEtag,
      payload,
      values,
      wasUncertain: false,
    };

    void executeUpdate(record);
  }, [currentEtag, executeUpdate, loadedSolicitud, submitStatus, values]);

  const retryUncertainUpdate = useCallback(() => {
    if (isSubmittingRef.current || submitStatus === 'submitting') return;
    const record = pendingUpdateRef.current;
    if (!record) {
      setSubmitStatus('idle');
      return;
    }
    void executeUpdate({ ...record, wasUncertain: true });
  }, [executeUpdate, submitStatus]);

  const cancelConfirmSave = useCallback(() => {
    if (submitStatus === 'submitting') return;
    setModalState('none');
    setSubmitStatus('idle');
  }, [submitStatus]);

  const closeConflictModal = useCallback(() => {
    if (isReloadingDatos) return;
    setModalState('none');
  }, [isReloadingDatos]);

  const reloadAfterConflict = useCallback(async () => {
    if (isReloadingDatos || submitStatus === 'submitting') return;

    reloadSeqRef.current += 1;
    const seq = reloadSeqRef.current;
    setIsReloadingDatos(true);
    setReloadError(null);
    setServerBannerError(null);

    try {
      const snapshot = await obtenerSolicitudParaEdicion(folioReal);
      if (seq !== reloadSeqRef.current) return;

      const mapped = mapSolicitudEdicionToFormValues(snapshot.solicitud);
      pendingUpdateRef.current = null;
      setLoadedSolicitud(snapshot.solicitud);
      setCurrentEtag(snapshot.etag);
      setBaselineValues(mapped);
      setValues(mapped);
      setTouchedFields({});
      setHasAttemptedSubmit(false);
      setServerFieldErrors({});
      setIsConcurrencyStale(false);
      setModalState('none');
      setSubmitStatus('idle');

      queryClient.setQueryData(['solicitudes', 'edicion', snapshot.solicitud.folio], snapshot);
      if (routeFolio !== snapshot.solicitud.folio) {
        queryClient.setQueryData(['solicitudes', 'edicion', routeFolio], snapshot);
      }

      showToast('info', 'Datos actualizados a la versión vigente');
    } catch (error) {
      if (seq !== reloadSeqRef.current) return;

      if (error instanceof HttpClientError) {
        if (error.statusCode === 404 || error.statusCode === 410) {
          setModalState('not-found');
          return;
        }
        if (error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
          setModalState('none');
          setServerBannerError('Tu sesión expiró. Inicia sesión nuevamente para continuar.');
          return;
        }
      }

      setModalState('none');
      setReloadError('No se pudo recargar la solicitud. Intenta nuevamente.');
    } finally {
      if (seq === reloadSeqRef.current) {
        setIsReloadingDatos(false);
      }
    }
  }, [folioReal, isReloadingDatos, queryClient, routeFolio, showToast, submitStatus]);

  const requestCancel = useCallback(() => {
    if (submitStatus === 'submitting' || isReloadingDatos) return;
    pendingExitDestinationRef.current = '/admin/dashboard';
    if (submitStatus === 'uncertain') {
      setModalState('confirm-uncertain-exit');
      return;
    }
    setModalState('confirm-discard');
  }, [isReloadingDatos, submitStatus]);

  const requestExitToDashboard = useCallback(() => {
    if (submitStatus === 'submitting' || isReloadingDatos) return;
    pendingExitDestinationRef.current = '/admin/dashboard';
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
  }, [isDirty, isReloadingDatos, navigate, submitStatus]);

  const requestExitToSolicitudes = useCallback(() => {
    if (submitStatus === 'submitting' || isReloadingDatos) return;
    if (submitStatus === 'uncertain') {
      pendingExitDestinationRef.current = '/admin/solicitudes';
      setModalState('confirm-uncertain-exit');
      return;
    }
    if (isDirty && submitStatus !== 'succeeded') {
      pendingExitDestinationRef.current = '/admin/solicitudes';
      setModalState('confirm-discard');
      return;
    }
    pendingExitDestinationRef.current = '/admin/dashboard';
    allowExitRef.current = true;
    navigate('/admin/solicitudes');
  }, [isDirty, isReloadingDatos, navigate, submitStatus]);

  const closeDiscardModal = useCallback(() => {
    pendingExitDestinationRef.current = '/admin/dashboard';
    setModalState('none');
  }, []);

  const confirmDiscardExit = useCallback(() => {
    const destination = pendingExitDestinationRef.current;
    pendingExitDestinationRef.current = '/admin/dashboard';
    allowExitRef.current = true;
    setModalState('none');
    navigate(destination);
  }, [navigate]);

  const confirmExitToDashboard = useCallback(() => {
    pendingExitDestinationRef.current = '/admin/dashboard';
    allowExitRef.current = true;
    setModalState('none');
    navigate('/admin/dashboard');
  }, [navigate]);

  const continueEditing = useCallback(() => {
    dismissToast();
    setConfirmacion(null);
    setModalState('none');
    setSubmitStatus('idle');
  }, [dismissToast]);

  return {
    folioReal,
    catalogos: catalogosQuery.data,
    isPreloading,
    isHydrated,
    initialLoadErrorType,
    isNotFoundModalOpen,
    selectableEstatusEntrega,
    selectableTiposAcuse,
    values,
    diasRestantes,
    diasRestantesTone,
    visibleErrors,
    invalidCount,
    showValidationSummary: isHydrated && hasAttemptedSubmit && invalidCount > 0,
    serverBannerError,
    submitStatus,
    isFormLocked,
    isUncertain,
    isSaveDisabled,
    isConcurrencyStale,
    isReloadingDatos,
    reloadError,
    modalState,
    confirmacion,
    toast,
    dismissToast,
    registerFieldRef,
    updateField,
    handleFieldBlur,
    handleTieneAcuseChange,
    retryInitialLoad,
    requestSubmit,
    confirmSave,
    retryUncertainUpdate,
    cancelConfirmSave,
    closeConflictModal,
    reloadAfterConflict,
    requestCancel,
    requestExitToDashboard,
    requestExitToSolicitudes,
    closeDiscardModal,
    confirmDiscardExit,
    confirmExitToDashboard,
    continueEditing,
  };
}
