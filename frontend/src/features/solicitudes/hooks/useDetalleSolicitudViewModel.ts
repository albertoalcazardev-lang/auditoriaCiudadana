import { useCallback, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { HttpClientError } from '../../../core/api/httpClient';
import { clearCsrfToken } from '../../auth/api/authApi';
import { sessionKey } from '../../auth/hooks/useSessionViewModel';
import {
  fetchSolicitudesCatalogos,
  obtenerSolicitudParaEdicion,
} from '../api/solicitudesApi';
import { formatIsoToDdMmYyyy } from './solicitudesDateUtils';
import {
  formatEstatusEfectivoLabel,
  formatMedioEntregaLabel,
  getDiasChipModifier,
  getEstatusBadgeModifier,
} from './solicitudesListadoUtils';

export type DetalleLoadErrorType = 'none' | 'b04' | 'unauthorized' | 'forbidden';

export function useDetalleSolicitudViewModel(routeFolio: string) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const catalogosQuery = useQuery({
    queryKey: ['solicitudes', 'catalogos'],
    queryFn: async ({ signal }) => {
      try {
        return await fetchSolicitudesCatalogos(signal);
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

  const detalleQuery = useQuery({
    queryKey: ['solicitudes', 'detalle', routeFolio],
    queryFn: async ({ signal }) => {
      try {
        return await obtenerSolicitudParaEdicion(routeFolio, signal);
      } catch (error) {
        if (error instanceof HttpClientError && error.statusCode === 401) {
          clearCsrfToken();
          void queryClient.invalidateQueries({ queryKey: sessionKey });
        }
        throw error;
      }
    },
    staleTime: 0,
    refetchOnMount: 'always',
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  const isLoading =
    detalleQuery.isFetching ||
    !detalleQuery.isFetchedAfterMount ||
    catalogosQuery.isFetching;

  const isNotFound = useMemo(() => {
    if (isLoading || !detalleQuery.isError) return false;
    return (
      detalleQuery.error instanceof HttpClientError &&
      (detalleQuery.error.statusCode === 404 || detalleQuery.error.statusCode === 410)
    );
  }, [detalleQuery.error, detalleQuery.isError, isLoading]);

  const loadErrorType: DetalleLoadErrorType = useMemo(() => {
    if (isLoading || isNotFound) return 'none';
    const activeError = detalleQuery.isError
      ? detalleQuery.error
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
    detalleQuery.error,
    detalleQuery.isError,
    isLoading,
    isNotFound,
  ]);

  const detalle = useMemo(() => {
    if (
      isLoading ||
      isNotFound ||
      loadErrorType !== 'none' ||
      !detalleQuery.data ||
      !catalogosQuery.data
    ) {
      return null;
    }

    const { solicitud } = detalleQuery.data;
    const catalogos = catalogosQuery.data;

    const areaReceptoraNombre =
      catalogos.areasReceptoras.find((a) => a.id === solicitud.areaReceptoraId)?.nombre ??
      solicitud.areaReceptoraId;

    const medioEntregaNombre =
      catalogos.mediosEntrega.find((m) => m.codigo === solicitud.medioEntrega)?.nombre ??
      formatMedioEntregaLabel(solicitud.medioEntrega);

    const tipoSolicitudNombre =
      catalogos.tiposSolicitud.find((t) => t.codigo === solicitud.tipoSolicitud)?.nombre ??
      solicitud.tipoSolicitud;

    const tipoEvidenciaNombre =
      catalogos.tiposEvidencia.find((e) => e.codigo === solicitud.tipoEvidencia)?.nombre ??
      solicitud.tipoEvidencia;

    const tipoAcuseNombre = !solicitud.tieneAcuse
      ? 'No aplica'
      : (catalogos.tiposAcuse.find((a) => a.codigo === solicitud.tipoAcuse)?.nombre ??
        solicitud.tipoAcuse);

    const isEntregada = solicitud.estatusEfectivo === 'Entregada';
    const diasSanitizados = Math.max(0, solicitud.diasRestantes);

    return {
      folio: solicitud.folio,
      asunto: solicitud.asunto,
      fechaEntregaFormatted: formatIsoToDdMmYyyy(solicitud.fechaEntrega),
      horaEntrega: solicitud.horaEntrega,
      fechaVencimientoFormatted: formatIsoToDdMmYyyy(solicitud.fechaVencimiento),
      estatusEfectivo: solicitud.estatusEfectivo,
      estatusLabel: formatEstatusEfectivoLabel(solicitud.estatusEfectivo),
      estatusModifier: getEstatusBadgeModifier(solicitud.estatusEfectivo),
      isEntregada,
      diasRestantes: diasSanitizados,
      diasLabel: isEntregada
        ? '—'
        : `${diasSanitizados} ${diasSanitizados === 1 ? 'día' : 'días'}`,
      diasModifier: getDiasChipModifier(diasSanitizados),
      areaReceptoraNombre,
      medioEntregaNombre,
      tipoSolicitudNombre,
      tipoEvidenciaNombre,
      tieneAcuseTexto: solicitud.tieneAcuse ? 'Sí' : 'No',
      tipoAcuseNombre,
    };
  }, [catalogosQuery.data, detalleQuery.data, isLoading, isNotFound, loadErrorType]);

  const retryLoad = useCallback(() => {
    if (detalleQuery.isError || !detalleQuery.data || !detalleQuery.isFetchedAfterMount) {
      void detalleQuery.refetch();
    }
    if (catalogosQuery.isError || !catalogosQuery.data) {
      void catalogosQuery.refetch();
    }
  }, [catalogosQuery, detalleQuery]);

  const volverAlListado = useCallback(() => {
    navigate('/admin/solicitudes');
  }, [navigate]);

  const irADashboard = useCallback(() => {
    navigate('/admin/dashboard');
  }, [navigate]);

  return {
    folioTitle: detalle?.folio ?? routeFolio,
    isLoading,
    isNotFound,
    loadErrorType,
    detalle,
    retryLoad,
    volverAlListado,
    irADashboard,
  };
}
