import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { HttpClientError } from '../../../core/api/httpClient';
import { clearCsrfToken } from '../../auth/api/authApi';
import { sessionKey } from '../../auth/hooks/useSessionViewModel';
import {
  DeleteResponseLostError,
  eliminarSolicitud,
  formatStrongETag,
  listarSolicitudes,
  obtenerSolicitudParaEdicion,
} from '../api/solicitudesApi';
import type {
  DeleteFeedbackBanner,
  SolicitudFilterChipKey,
  SolicitudListadoItem,
  SolicitudPageSize,
  SolicitudSortField,
  SolicitudToastMessage,
  SolicitudesFilterDraft,
  SolicitudesFilterErrors,
  SolicitudesUrlQueryState,
} from '../types/solicitudes.types';
import {
  EMPTY_FILTER_DRAFT,
  buildActiveFilterChips,
  buildFilterDraftFromState,
  buildSolicitudesSearchParams,
  isImpossibleCalendarDateQuery,
  parseSolicitudesSearchParams,
  toListarSolicitudesApiParams,
  validateFilterDraft,
} from './solicitudesListadoUtils';

const SEARCH_DEBOUNCE_MS = 300;

export type ListadoLoadErrorType = 'none' | 'b04' | 'unauthorized' | 'forbidden';

function readInitialMobileViewport(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return false;
  }
  return window.matchMedia('(max-width: 768px)').matches;
}

export function useSolicitudesListadoViewModel() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const urlState = useMemo(
    () => parseSolicitudesSearchParams(searchParams),
    [searchParams],
  );
  const urlStateRef = useRef<SolicitudesUrlQueryState>(urlState);
  urlStateRef.current = urlState;

  const [searchInput, setSearchInput] = useState(urlState.q);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const debounceTimerRef = useRef<number | null>(null);

  const [isFiltersOpen, setIsFiltersOpen] = useState(false);
  const [filterDraft, setFilterDraft] = useState<SolicitudesFilterDraft>(() =>
    buildFilterDraftFromState(urlState),
  );
  const [filterErrors, setFilterErrors] = useState<SolicitudesFilterErrors>({});

  const [deleteTarget, setDeleteTarget] = useState<SolicitudListadoItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const isDeletingRef = useRef(false);
  const [deleteFeedback, setDeleteFeedback] = useState<DeleteFeedbackBanner | null>(null);
  const [locallyRemovedIds, setLocallyRemovedIds] = useState<string[]>([]);

  const [toast, setToast] = useState<SolicitudToastMessage | null>(null);
  const toastTimerRef = useRef<number | null>(null);
  const toastSeqRef = useRef(0);

  const [isMobileViewport, setIsMobileViewport] = useState<boolean>(readInitialMobileViewport);

  const lastPaginationMetaRef = useRef<{
    totalItems: number;
    totalPages: number;
    page: number;
    pageSize: number;
  } | null>(null);

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return;
    }
    const mql = window.matchMedia('(max-width: 768px)');
    setIsMobileViewport(mql.matches);
    const onChange = (event: MediaQueryListEvent) => {
      setIsMobileViewport(event.matches);
    };
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, []);

  useEffect(() => {
    return () => {
      if (debounceTimerRef.current !== null) {
        window.clearTimeout(debounceTimerRef.current);
      }
      if (toastTimerRef.current !== null) {
        window.clearTimeout(toastTimerRef.current);
      }
    };
  }, []);

  const clearPendingSearchDebounce = useCallback(() => {
    if (debounceTimerRef.current !== null) {
      window.clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }
  }, []);

  useEffect(() => {
    clearPendingSearchDebounce();
    setSearchInput(urlState.q);
  }, [clearPendingSearchDebounce, urlState.q]);

  useEffect(() => {
    const handlePopState = () => {
      clearPendingSearchDebounce();
      const nextQ = (new URLSearchParams(window.location.search).get('q') ?? '').trim();
      setSearchInput(nextQ);
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [clearPendingSearchDebounce]);

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

  const dismissDeleteFeedback = useCallback(() => {
    setDeleteFeedback(null);
  }, []);

  const applyUrlState = useCallback(
    (nextState: SolicitudesUrlQueryState, replace = false) => {
      const nextParams = buildSolicitudesSearchParams(nextState);
      setSearchParams(nextParams, { replace });
    },
    [setSearchParams],
  );

  const commitSearchText = useCallback(
    (rawText: string) => {
      const trimmed = rawText.trim();
      const current = urlStateRef.current;
      if (trimmed === current.q) return;
      applyUrlState({
        ...current,
        q: trimmed,
        page: 1,
      });
    },
    [applyUrlState],
  );

  const handleSearchInputChange = useCallback(
    (nextValue: string) => {
      setSearchInput(nextValue);
      clearPendingSearchDebounce();
      debounceTimerRef.current = window.setTimeout(() => {
        debounceTimerRef.current = null;
        commitSearchText(nextValue);
      }, SEARCH_DEBOUNCE_MS);
    },
    [clearPendingSearchDebounce, commitSearchText],
  );

  const handleSearchEnter = useCallback(() => {
    clearPendingSearchDebounce();
    commitSearchText(searchInput);
  }, [clearPendingSearchDebounce, commitSearchText, searchInput]);

  const handleSearchClear = useCallback(() => {
    clearPendingSearchDebounce();
    setSearchInput('');
    commitSearchText('');
    searchInputRef.current?.focus();
  }, [clearPendingSearchDebounce, commitSearchText]);

  const toggleFiltersPopover = useCallback(() => {
    if (isFiltersOpen) {
      setIsFiltersOpen(false);
      setFilterErrors({});
      return;
    }
    setFilterDraft(buildFilterDraftFromState(urlStateRef.current));
    setFilterErrors({});
    setIsFiltersOpen(true);
  }, [isFiltersOpen]);

  const closeFiltersPopover = useCallback(() => {
    setIsFiltersOpen(false);
    setFilterErrors({});
  }, []);

  const updateFilterDraftField = useCallback(
    <K extends keyof SolicitudesFilterDraft>(field: K, value: SolicitudesFilterDraft[K]) => {
      setFilterDraft((prev) => {
        if (field === 'dias' && value !== 'custom') {
          return { ...prev, dias: value as SolicitudesFilterDraft['dias'], diasMin: '', diasMax: '' };
        }
        return { ...prev, [field]: value };
      });
      setFilterErrors((prev) => {
        const next = { ...prev };
        if (field === 'entDesde' || field === 'entHasta') {
          delete next.entDesde;
          delete next.entHasta;
          delete next.entRange;
        } else if (field === 'venDesde' || field === 'venHasta') {
          delete next.venDesde;
          delete next.venHasta;
          delete next.venRange;
        } else if (field === 'dias' || field === 'diasMin' || field === 'diasMax') {
          delete next.diasCustom;
        }
        return next;
      });
    },
    [],
  );

  const clearPopoverFilters = useCallback(() => {
    setFilterDraft(EMPTY_FILTER_DRAFT);
    setFilterErrors({});
    const current = urlStateRef.current;
    const hadPopoverFilters =
      Boolean(current.ent_d) ||
      Boolean(current.ent_h) ||
      Boolean(current.ven_d) ||
      Boolean(current.ven_h) ||
      current.dias !== 'todos';

    if (hadPopoverFilters) {
      applyUrlState({
        ...current,
        ent_d: '',
        ent_h: '',
        ven_d: '',
        ven_h: '',
        dias: 'todos',
        dias_min: undefined,
        dias_max: undefined,
        page: 1,
      });
    }
  }, [applyUrlState]);

  const applyPopoverFilters = useCallback(() => {
    const { errors, isValid, isoValues } = validateFilterDraft(filterDraft);
    if (!isValid) {
      setFilterErrors(errors);
      return;
    }

    setFilterErrors({});
    setIsFiltersOpen(false);
    const current = urlStateRef.current;
    applyUrlState({
      ...current,
      ent_d: isoValues.ent_d,
      ent_h: isoValues.ent_h,
      ven_d: isoValues.ven_d,
      ven_h: isoValues.ven_h,
      dias: isoValues.dias,
      dias_min: isoValues.dias_min,
      dias_max: isoValues.dias_max,
      page: 1,
    });
  }, [applyUrlState, filterDraft]);

  const removeFilterChip = useCallback(
    (key: SolicitudFilterChipKey) => {
      const current = urlStateRef.current;
      if (key === 'entrega') {
        applyUrlState({ ...current, ent_d: '', ent_h: '', page: 1 });
      } else if (key === 'vencimiento') {
        applyUrlState({ ...current, ven_d: '', ven_h: '', page: 1 });
      } else if (key === 'dias') {
        applyUrlState({
          ...current,
          dias: 'todos',
          dias_min: undefined,
          dias_max: undefined,
          page: 1,
        });
      }
    },
    [applyUrlState],
  );

  const clearActiveFilters = useCallback(() => {
    const current = urlStateRef.current;
    setFilterDraft(EMPTY_FILTER_DRAFT);
    setFilterErrors({});
    applyUrlState({
      ...current,
      ent_d: '',
      ent_h: '',
      ven_d: '',
      ven_h: '',
      dias: 'todos',
      dias_min: undefined,
      dias_max: undefined,
      page: 1,
    });
  }, [applyUrlState]);

  const clearAllCriteria = useCallback(() => {
    clearPendingSearchDebounce();
    setSearchInput('');
    setFilterDraft(EMPTY_FILTER_DRAFT);
    setFilterErrors({});
    const current = urlStateRef.current;
    applyUrlState({
      ...current,
      q: '',
      ent_d: '',
      ent_h: '',
      ven_d: '',
      ven_h: '',
      dias: 'todos',
      dias_min: undefined,
      dias_max: undefined,
      page: 1,
    });
  }, [applyUrlState, clearPendingSearchDebounce]);

  const handleSort = useCallback(
    (field: SolicitudSortField) => {
      const current = urlStateRef.current;
      const nextDir =
        current.sort === field ? (current.dir === 'asc' ? 'desc' : 'asc') : 'asc';
      applyUrlState({
        ...current,
        sort: field,
        dir: nextDir,
        page: 1,
      });
    },
    [applyUrlState],
  );

  const handlePageChange = useCallback(
    (nextPage: number) => {
      const current = urlStateRef.current;
      if (nextPage < 1 || nextPage === current.page) return;
      applyUrlState({
        ...current,
        page: nextPage,
      });
    },
    [applyUrlState],
  );

  const handlePageSizeChange = useCallback(
    (nextSize: SolicitudPageSize) => {
      const current = urlStateRef.current;
      if (nextSize === current.size && current.page === 1) return;
      applyUrlState({
        ...current,
        size: nextSize,
        page: 1,
      });
    },
    [applyUrlState],
  );

  const isImpossibleSearchDate = useMemo(
    () => isImpossibleCalendarDateQuery(urlState.q),
    [urlState.q],
  );

  const apiParams = useMemo(() => toListarSolicitudesApiParams(urlState), [urlState]);

  const listadoQuery = useQuery({
    queryKey: ['solicitudes', 'listado', apiParams],
    queryFn: async ({ signal }) => {
      try {
        return await listarSolicitudes(apiParams, signal);
      } catch (error) {
        if (error instanceof HttpClientError && error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
        }
        throw error;
      }
    },
    enabled: !isImpossibleSearchDate,
    staleTime: 0,
    refetchOnMount: 'always',
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  useEffect(() => {
    if (listadoQuery.data && !listadoQuery.isFetching && !listadoQuery.isError) {
      setLocallyRemovedIds([]);
      lastPaginationMetaRef.current = {
        totalItems: listadoQuery.data.totalItems,
        totalPages: Math.max(1, listadoQuery.data.totalPages),
        page: listadoQuery.data.page,
        pageSize: listadoQuery.data.pageSize,
      };
    }
  }, [listadoQuery.data, listadoQuery.dataUpdatedAt, listadoQuery.isError, listadoQuery.isFetching]);

  const isServerValidation400 = useMemo(() => {
    if (isImpossibleSearchDate || listadoQuery.isFetching || !listadoQuery.isError) {
      return false;
    }
    return (
      listadoQuery.error instanceof HttpClientError && listadoQuery.error.statusCode === 400
    );
  }, [isImpossibleSearchDate, listadoQuery.error, listadoQuery.isError, listadoQuery.isFetching]);

  const friendlyCriteriaHint = useMemo(() => {
    if (isImpossibleSearchDate) {
      return `La fecha "${urlState.q}" no es una fecha válida del calendario (dd/mm/aaaa).`;
    }
    if (isServerValidation400) {
      return 'El criterio ingresado no tiene un formato válido. Revisa o limpia la búsqueda y filtros.';
    }
    return null;
  }, [isImpossibleSearchDate, isServerValidation400, urlState.q]);

  const loadErrorType: ListadoLoadErrorType = useMemo(() => {
    if (
      isImpossibleSearchDate ||
      isServerValidation400 ||
      listadoQuery.isFetching ||
      !listadoQuery.isFetchedAfterMount ||
      !listadoQuery.isError
    ) {
      return 'none';
    }
    if (listadoQuery.error instanceof HttpClientError) {
      if (listadoQuery.error.statusCode === 401) return 'unauthorized';
      if (listadoQuery.error.statusCode === 403) return 'forbidden';
    }
    return 'b04';
  }, [
    isImpossibleSearchDate,
    isServerValidation400,
    listadoQuery.error,
    listadoQuery.isError,
    listadoQuery.isFetchedAfterMount,
    listadoQuery.isFetching,
  ]);

  const isOrphanPage = useMemo(() => {
    if (
      isImpossibleSearchDate ||
      !listadoQuery.isFetchedAfterMount ||
      listadoQuery.isFetching ||
      listadoQuery.isError ||
      !listadoQuery.data
    ) {
      return false;
    }
    const safeTotalPages = Math.max(1, listadoQuery.data.totalPages);
    return (
      listadoQuery.data.totalItems > 0 &&
      listadoQuery.data.items.length === 0 &&
      urlState.page > safeTotalPages
    );
  }, [
    isImpossibleSearchDate,
    listadoQuery.data,
    listadoQuery.isError,
    listadoQuery.isFetchedAfterMount,
    listadoQuery.isFetching,
    urlState.page,
  ]);

  useEffect(() => {
    if (!isOrphanPage || !listadoQuery.data) return;
    const safeTotalPages = Math.max(1, listadoQuery.data.totalPages);
    applyUrlState(
      {
        ...urlStateRef.current,
        page: safeTotalPages,
      },
      true,
    );
  }, [applyUrlState, isOrphanPage, listadoQuery.data]);

  const isLoading =
    !isImpossibleSearchDate &&
    (listadoQuery.isFetching || !listadoQuery.isFetchedAfterMount || isOrphanPage);

  const activeChips = useMemo(() => buildActiveFilterChips(urlState), [urlState]);
  const activeFilterCount = activeChips.length;
  const hasAnyActiveCriteria = Boolean(urlState.q) || activeChips.length > 0;

  const visibleItems = useMemo(() => {
    if (
      isLoading ||
      loadErrorType !== 'none' ||
      isImpossibleSearchDate ||
      isServerValidation400 ||
      !listadoQuery.data
    ) {
      return [];
    }
    if (locallyRemovedIds.length === 0) {
      return listadoQuery.data.items;
    }
    const removedSet = new Set(locallyRemovedIds);
    return listadoQuery.data.items.filter((item) => !removedSet.has(item.id));
  }, [
    isImpossibleSearchDate,
    isLoading,
    isServerValidation400,
    listadoQuery.data,
    loadErrorType,
    locallyRemovedIds,
  ]);

  const effectiveTotalItems = useMemo(() => {
    if (isImpossibleSearchDate || isServerValidation400) return 0;
    if (!isLoading && loadErrorType === 'none' && listadoQuery.data) {
      return Math.max(0, listadoQuery.data.totalItems - locallyRemovedIds.length);
    }
    if (isLoading && lastPaginationMetaRef.current) {
      return lastPaginationMetaRef.current.totalItems;
    }
    return 0;
  }, [
    isImpossibleSearchDate,
    isLoading,
    isServerValidation400,
    listadoQuery.data,
    loadErrorType,
    locallyRemovedIds.length,
  ]);

  const effectiveTotalPages = useMemo(() => {
    if (isImpossibleSearchDate || isServerValidation400) return 1;
    if (!isLoading && loadErrorType === 'none' && listadoQuery.data) {
      return Math.max(1, listadoQuery.data.totalPages);
    }
    if (isLoading && lastPaginationMetaRef.current) {
      return lastPaginationMetaRef.current.totalPages;
    }
    return 1;
  }, [
    isImpossibleSearchDate,
    isLoading,
    isServerValidation400,
    listadoQuery.data,
    loadErrorType,
  ]);

  const isEmptyNoMatches =
    !isLoading &&
    loadErrorType === 'none' &&
    (isImpossibleSearchDate ||
      isServerValidation400 ||
      (effectiveTotalItems === 0 && hasAnyActiveCriteria));

  const isEmptyNoSolicitudes =
    !isLoading &&
    loadErrorType === 'none' &&
    !isImpossibleSearchDate &&
    !isServerValidation400 &&
    effectiveTotalItems === 0 &&
    !hasAnyActiveCriteria;

  const showPagination =
    loadErrorType === 'none' &&
    !isImpossibleSearchDate &&
    !isServerValidation400 &&
    !isEmptyNoMatches &&
    !isEmptyNoSolicitudes &&
    (effectiveTotalItems > 0 || (isLoading && lastPaginationMetaRef.current !== null));

  const refreshListado = useCallback(() => {
    setDeleteFeedback(null);
    setLocallyRemovedIds([]);
    if (!isImpossibleSearchDate) {
      void listadoQuery.refetch();
    }
  }, [isImpossibleSearchDate, listadoQuery]);

  const openDeleteModal = useCallback((item: SolicitudListadoItem) => {
    if (isDeletingRef.current) return;
    setDeleteFeedback(null);
    setDeleteTarget(item);
  }, []);

  const closeDeleteModal = useCallback(() => {
    if (isDeletingRef.current) return;
    setDeleteTarget(null);
  }, []);

  const handleConfirmedDeletion = useCallback(
    async (target: SolicitudListadoItem) => {
      const current = urlStateRef.current;
      const currentItemsCount = visibleItems.length;
      const shouldStepBackPage = currentItemsCount <= 1 && current.page > 1;

      setDeleteTarget(null);
      setDeleteFeedback(null);

      if (shouldStepBackPage) {
        await queryClient.invalidateQueries({
          queryKey: ['solicitudes'],
          refetchType: 'none',
        });
        applyUrlState({
          ...current,
          page: current.page - 1,
        });
      } else {
        await queryClient.invalidateQueries({ queryKey: ['solicitudes'] });
      }

      showToast('success', `Solicitud ${target.folio} eliminada.`);
    },
    [applyUrlState, queryClient, showToast, visibleItems.length],
  );

  const confirmDelete = useCallback(async () => {
    if (!deleteTarget || isDeletingRef.current) return;

    const target = deleteTarget;
    isDeletingRef.current = true;
    setIsDeleting(true);
    setDeleteFeedback(null);

    try {
      await eliminarSolicitud(target.id, target.version);
      await handleConfirmedDeletion(target);
    } catch (error) {
      if (error instanceof DeleteResponseLostError) {
        // Regla crítica: verificar inmediatamente estado real con GET /solicitudes/edicion?folio=...
        try {
          const snapshot = await obtenerSolicitudParaEdicion(target.folio);
          const expectedEtag = formatStrongETag(target.version);
          const currentServerEtag = formatStrongETag(snapshot.etag);

          setDeleteTarget(null);
          if (currentServerEtag !== expectedEtag) {
            setDeleteFeedback({
              tone: 'warning',
              message: `La solicitud ${target.folio} cambió desde que fue consultada. Actualiza el listado para obtener la versión vigente.`,
              canRefreshList: true,
            });
          } else {
            setDeleteFeedback({
              tone: 'error',
              message: `Se interrumpió la conexión durante la operación y la solicitud ${target.folio} continúa registrada. Inténtalo de nuevo.`,
              canRefreshList: false,
            });
          }
        } catch (verifyError) {
          if (verifyError instanceof HttpClientError) {
            if (verifyError.statusCode === 404 || verifyError.statusCode === 410) {
              await handleConfirmedDeletion(target);
              return;
            }
            if (verifyError.statusCode === 401) {
              clearCsrfToken();
              void queryClient.invalidateQueries({ queryKey: sessionKey });
              setDeleteTarget(null);
              return;
            }
          }

          setDeleteTarget(null);
          setDeleteFeedback({
            tone: 'warning',
            message: `No se pudo confirmar el resultado de la eliminación de ${target.folio} por pérdida de conexión. Actualiza el listado para verificar su estado.`,
            canRefreshList: true,
          });
        }
        return;
      }

      if (error instanceof HttpClientError) {
        if (error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
          setDeleteTarget(null);
          return;
        }

        if (error.statusCode === 404 || error.statusCode === 410) {
          setDeleteTarget(null);
          setLocallyRemovedIds((prev) =>
            prev.includes(target.id) ? prev : [...prev, target.id],
          );
          setDeleteFeedback({
            tone: 'info',
            message: `La solicitud ${target.folio} ya no existe. Puedes actualizar el listado para sincronizar los resultados.`,
            canRefreshList: true,
          });
          return;
        }

        if (error.statusCode === 409) {
          setDeleteTarget(null);
          setDeleteFeedback({
            tone: 'error',
            message: `No se puede eliminar la solicitud ${target.folio} porque existen datos relacionados que impiden eliminarla.`,
            canRefreshList: false,
          });
          return;
        }

        if (error.statusCode === 412 || error.statusCode === 428) {
          setDeleteTarget(null);
          setDeleteFeedback({
            tone: 'warning',
            message: `La solicitud ${target.folio} cambió desde que fue consultada. Actualiza el listado para ver la versión vigente.`,
            canRefreshList: true,
          });
          return;
        }

        if (error.statusCode === 403) {
          setDeleteTarget(null);
          setDeleteFeedback({
            tone: 'error',
            message: `No tienes permisos para eliminar la solicitud ${target.folio}.`,
            canRefreshList: false,
          });
          return;
        }

        if (error.statusCode === 400) {
          clearCsrfToken();
          setDeleteTarget(null);
          setDeleteFeedback({
            tone: 'error',
            message: `No se pudo validar la solicitud de eliminación o el token de seguridad expiró. Inténtalo nuevamente.`,
            canRefreshList: false,
          });
          return;
        }

        setDeleteTarget(null);
        setDeleteFeedback({
          tone: 'error',
          message: `El servicio no está disponible temporalmente. La solicitud ${target.folio} no fue eliminada; inténtalo más tarde.`,
          canRefreshList: true,
        });
        return;
      }

      setDeleteTarget(null);
      setDeleteFeedback({
        tone: 'error',
        message: `No fue posible conectar con el servidor para eliminar la solicitud ${target.folio}. Inténtalo nuevamente.`,
        canRefreshList: false,
      });
    } finally {
      isDeletingRef.current = false;
      setIsDeleting(false);
    }
  }, [deleteTarget, handleConfirmedDeletion, queryClient]);

  const goToNuevaSolicitud = useCallback(() => {
    clearPendingSearchDebounce();
    navigate('/admin/solicitudes/nueva');
  }, [clearPendingSearchDebounce, navigate]);

  const goToVerSolicitud = useCallback(
    (folio: string) => {
      clearPendingSearchDebounce();
      navigate(`/admin/solicitudes/${encodeURIComponent(folio)}`);
    },
    [clearPendingSearchDebounce, navigate],
  );

  const goToEditarSolicitud = useCallback(
    (folio: string) => {
      clearPendingSearchDebounce();
      navigate(`/admin/solicitudes/${encodeURIComponent(folio)}/editar`);
    },
    [clearPendingSearchDebounce, navigate],
  );

  const goToDashboard = useCallback(() => {
    clearPendingSearchDebounce();
    navigate('/admin/dashboard');
  }, [clearPendingSearchDebounce, navigate]);

  const goToSolicitudes = useCallback(() => {
    clearPendingSearchDebounce();
    setSearchInput('');
    setIsFiltersOpen(false);
    setFilterErrors({});
    navigate('/admin/solicitudes');
  }, [clearPendingSearchDebounce, navigate]);

  return {
    urlState,
    searchInput,
    searchInputRef,
    handleSearchInputChange,
    handleSearchEnter,
    handleSearchClear,
    friendlyCriteriaHint,

    isFiltersOpen,
    filterDraft,
    filterErrors,
    toggleFiltersPopover,
    closeFiltersPopover,
    updateFilterDraftField,
    clearPopoverFilters,
    applyPopoverFilters,

    activeChips,
    activeFilterCount,
    hasAnyActiveCriteria,
    removeFilterChip,
    clearActiveFilters,
    clearAllCriteria,

    handleSort,
    handlePageChange,
    handlePageSizeChange,

    isLoading,
    loadErrorType,
    isEmptyNoMatches,
    isEmptyNoSolicitudes,
    showPagination,
    items: visibleItems,
    totalItems: effectiveTotalItems,
    totalPages: effectiveTotalPages,
    currentPage: urlState.page,
    pageSize: urlState.size,
    isMobileViewport,
    refreshListado,

    deleteTarget,
    isDeleting,
    deleteFeedback,
    dismissDeleteFeedback,
    openDeleteModal,
    closeDeleteModal,
    confirmDelete,

    toast,
    dismissToast,

    goToNuevaSolicitud,
    goToVerSolicitud,
    goToEditarSolicitud,
    goToDashboard,
    goToSolicitudes,
  };
}
