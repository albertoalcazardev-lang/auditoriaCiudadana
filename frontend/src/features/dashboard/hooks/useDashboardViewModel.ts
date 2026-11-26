import { useCallback, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { HttpClientError } from '../../../core/api/httpClient';
import { clearCsrfToken } from '../../auth/api/authApi';
import { sessionKey } from '../../auth/hooks/useSessionViewModel';
import { obtenerDashboard } from '../api/dashboardApi';
import {
  PERIODO_OPTIONS,
  type AreaBarChartViewModel,
  type AreaBarItemViewModel,
  type DashboardAreaItem,
  type DashboardIndicadores,
  type DashboardResponse,
  type KpiCardId,
  type KpiCardViewModel,
  type PeriodoDashboard,
  type RecentActivityViewModel,
  type StatusDonutViewModel,
  type UpcomingDeadlinesViewModel,
} from '../types/dashboard.types';

const KPI_BASE_CONFIG: ReadonlyArray<{
  id: KpiCardId;
  label: string;
  iconBg: string;
  iconColor: string;
}> = [
  { id: 'total', label: 'Total solicitudes', iconBg: '#EAF1FF', iconColor: '#1D4ED8' },
  { id: 'en-proceso', label: 'En proceso', iconBg: '#FFEDD5', iconColor: '#C2410C' },
  { id: 'entregadas', label: 'Entregadas', iconBg: '#DCFCE7', iconColor: '#15803D' },
  { id: 'vencidas', label: 'Vencidas', iconBg: '#FEE2E2', iconColor: '#B91C1C' },
  { id: 'proximas-a-vencer', label: 'Próximas a vencer', iconBg: '#EDE9FE', iconColor: '#6D28D9' },
];

function isNonNegativeInt(value: unknown): value is number {
  return (
    typeof value === 'number' &&
    Number.isFinite(value) &&
    Number.isInteger(value) &&
    value >= 0
  );
}

function isValidIsoDateOnly(value: unknown): value is string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }
  const [yStr, mStr, dStr] = value.split('-');
  const year = Number(yStr);
  const month = Number(mStr);
  const day = Number(dStr);
  const dt = new Date(Date.UTC(year, month - 1, day));
  return (
    dt.getUTCFullYear() === year &&
    dt.getUTCMonth() === month - 1 &&
    dt.getUTCDate() === day
  );
}

function formatIsoDateToDdMmYyyy(iso: string): string {
  const [yyyy, mm, dd] = iso.split('-');
  return `${dd}/${mm}/${yyyy}`;
}

function formatDecimal1(value: number): string {
  const rounded = Math.round(value * 10) / 10;
  if (Object.is(rounded, -0) || rounded === 0) {
    return '0';
  }
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

function formatShareText(part: number, total: number): string {
  if (total <= 0) return '0%';
  return `${formatDecimal1((part / total) * 100)}%`;
}

function formatVariacionText(variacion: number): string {
  const rounded = Math.round(variacion * 10) / 10;
  if (Object.is(rounded, -0) || rounded === 0) {
    return '0% vs. periodo anterior';
  }
  const prefix = rounded > 0 ? '+' : '';
  const numText = Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
  return `${prefix}${numText}% vs. periodo anterior`;
}

function formatActualizadoTexto(isoUtc: string | undefined | null): string | null {
  if (!isoUtc || typeof isoUtc !== 'string') return null;
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return null;

  const parts = new Intl.DateTimeFormat('es-MX', {
    timeZone: 'America/Mexico_City',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);

  const hour = parts.find((p) => p.type === 'hour')?.value;
  const minute = parts.find((p) => p.type === 'minute')?.value;
  if (!hour || !minute) return null;

  return `Actualizado a las ${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`;
}

export function formatTiempoRelativo(
  ocurridoEnUtc: string,
  referenciaUtc?: string | null,
): string {
  const ocurridoMs = new Date(ocurridoEnUtc).getTime();
  const refDate = referenciaUtc ? new Date(referenciaUtc) : new Date();
  const refMs = Number.isNaN(refDate.getTime()) ? Date.now() : refDate.getTime();
  const diffSeconds = Math.max(0, Math.floor((refMs - ocurridoMs) / 1000));

  if (diffSeconds < 60) {
    return 'hace unos instantes';
  }
  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) {
    return `hace ${diffMinutes} min`;
  }
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) {
    return `hace ${diffHours} h`;
  }
  const diffDays = Math.floor(diffHours / 24);
  return `hace ${diffDays} d`;
}

function validateIndicadores(data: DashboardResponse): DashboardIndicadores | null {
  if (data.disponibilidad?.indicadores === false) {
    return null;
  }
  const ind = data.indicadores;
  if (!ind || typeof ind !== 'object') {
    return null;
  }
  if (
    !isNonNegativeInt(ind.total) ||
    !isNonNegativeInt(ind.enProceso) ||
    !isNonNegativeInt(ind.entregadas) ||
    !isNonNegativeInt(ind.vencidas) ||
    !isNonNegativeInt(ind.proximasAVencer)
  ) {
    return null;
  }
  if (ind.total !== ind.enProceso + ind.entregadas + ind.vencidas) {
    return null;
  }
  if (ind.proximasAVencer > ind.enProceso) {
    return null;
  }
  return ind;
}

function normalizeAndGroupAreas(rawItems: DashboardAreaItem[]): AreaBarItemViewModel[] {
  const positiveItems = rawItems
    .map((item) => ({ area: item.area.trim(), cantidad: item.cantidad }))
    .filter((item) => item.cantidad > 0);

  if (positiveItems.length === 0) {
    return [];
  }

  let existingOtrasCantidad = 0;
  const regularItems: DashboardAreaItem[] = [];

  for (const item of positiveItems) {
    if (item.area.toLowerCase() === 'otras') {
      existingOtrasCantidad += item.cantidad;
    } else {
      regularItems.push(item);
    }
  }

  regularItems.sort((a, b) => {
    if (b.cantidad !== a.cantidad) {
      return b.cantidad - a.cantidad;
    }
    return a.area.localeCompare(b.area, 'es');
  });

  const topMain = regularItems.slice(0, 6);
  const overflowSum = regularItems.slice(6).reduce((sum, item) => sum + item.cantidad, 0);
  const totalOtras = existingOtrasCantidad + overflowSum;

  const finalItems: DashboardAreaItem[] =
    totalOtras > 0 ? [...topMain, { area: 'Otras', cantidad: totalOtras }] : topMain;

  return finalItems.map((item) => ({
    area: item.area,
    cantidad: item.cantidad,
    cantidadTexto: item.cantidad === 1 ? '1 solicitud' : `${item.cantidad} solicitudes`,
  }));
}

function buildScale(maxCantidad: number): {
  scaleMax: number;
  guideTicks: [number, number, number, number];
} {
  const step = Math.max(1, Math.ceil(maxCantidad / 4));
  const scaleMax = step * 4;
  return {
    scaleMax,
    guideTicks: [step, step * 2, step * 3, scaleMax],
  };
}

function buildLoadingKpis(): KpiCardViewModel[] {
  return KPI_BASE_CONFIG.map((cfg) => ({
    ...cfg,
    state: 'loading',
    valueText: '',
    secondaryText: null,
  }));
}

function buildErrorKpis(): KpiCardViewModel[] {
  return KPI_BASE_CONFIG.map((cfg) => ({
    ...cfg,
    state: 'error',
    valueText: '—',
    secondaryText: 'Información no disponible',
  }));
}

function buildReadyKpis(ind: DashboardIndicadores): KpiCardViewModel[] {
  const isEmpty = ind.total === 0;
  const hasVariacion =
    typeof ind.variacionTotalPorcentaje === 'number' &&
    Number.isFinite(ind.variacionTotalPorcentaje);

  const totalSecondary = isEmpty
    ? 'Sin datos'
    : hasVariacion
      ? formatVariacionText(ind.variacionTotalPorcentaje!)
      : null;

  return [
    {
      ...KPI_BASE_CONFIG[0],
      state: 'ready',
      valueText: String(ind.total),
      secondaryText: totalSecondary,
    },
    {
      ...KPI_BASE_CONFIG[1],
      state: 'ready',
      valueText: String(ind.enProceso),
      secondaryText: isEmpty ? 'Sin datos' : `${formatShareText(ind.enProceso, ind.total)} del total`,
    },
    {
      ...KPI_BASE_CONFIG[2],
      state: 'ready',
      valueText: String(ind.entregadas),
      secondaryText: isEmpty
        ? 'Sin datos'
        : `${formatShareText(ind.entregadas, ind.total)} del total`,
    },
    {
      ...KPI_BASE_CONFIG[3],
      state: 'ready',
      valueText: String(ind.vencidas),
      secondaryText: isEmpty ? 'Sin datos' : `${formatShareText(ind.vencidas, ind.total)} del total`,
    },
    {
      ...KPI_BASE_CONFIG[4],
      state: 'ready',
      valueText: String(ind.proximasAVencer),
      secondaryText: isEmpty ? 'Sin datos' : 'Próximos 7 días',
    },
  ];
}

function buildVerTodasHref(
  periodo: PeriodoDashboard,
  fechaInicio?: string | null,
  fechaFin?: string | null,
): string {
  if (
    periodo !== 'todo' &&
    isValidIsoDateOnly(fechaInicio) &&
    isValidIsoDateOnly(fechaFin)
  ) {
    return `/admin/solicitudes?dias=1-7&ent_d=${encodeURIComponent(fechaInicio)}&ent_h=${encodeURIComponent(fechaFin)}`;
  }
  return '/admin/solicitudes?dias=1-7';
}

export function useDashboardViewModel() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [periodo, setPeriodo] = useState<PeriodoDashboard>('este-mes');

  const query = useQuery({
    queryKey: ['dashboard', periodo] as const,
    queryFn: async ({ signal }) => {
      try {
        return await obtenerDashboard(periodo, signal);
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
    gcTime: 1_000,
    refetchOnWindowFocus: false,
  });

  const handlePeriodoChange = useCallback(
    (nextPeriodo: PeriodoDashboard) => {
      if (nextPeriodo === periodo) return;
      void queryClient.cancelQueries({ queryKey: ['dashboard'] });
      queryClient.removeQueries({ queryKey: ['dashboard'] });
      setPeriodo(nextPeriodo);
    },
    [periodo, queryClient],
  );

  const refresh = useCallback(() => {
    void query.refetch();
  }, [query]);

  const stayOnDashboard = useCallback(() => {
    // Ya estamos en /admin/dashboard
  }, []);

  const goToSolicitudes = useCallback(() => {
    navigate('/admin/solicitudes');
  }, [navigate]);

  const selectDeadlineFolio = useCallback(
    (folio: string) => {
      navigate(`/admin/solicitudes/${encodeURIComponent(folio)}`);
    },
    [navigate],
  );

  const viewState = useMemo(() => {
    const isInitialOrRetryLoading =
      query.isPending || (query.isFetching && (query.isError || !query.data));

    if (isInitialOrRetryLoading) {
      return {
        toolbarStatusText: 'Actualizando…',
        isTotalError: false,
        kpisState: 'loading' as const,
        kpis: buildLoadingKpis(),
        donut: { state: 'loading', total: 0, segments: [] } satisfies StatusDonutViewModel,
        deadlines: {
          state: 'loading',
          verTodasHref: '/admin/solicitudes?dias=1-7',
          rows: [],
        } satisfies UpcomingDeadlinesViewModel,
        areaChart: {
          state: 'loading',
          bars: [],
          scaleMax: 4,
          guideTicks: [1, 2, 3, 4],
        } satisfies AreaBarChartViewModel,
        recentActivity: {
          state: 'loading',
          items: [],
        } satisfies RecentActivityViewModel,
      };
    }

    const data = query.data;
    const isTopLevelInvalid =
      query.isError ||
      !data ||
      typeof data !== 'object' ||
      (data.periodo !== undefined && data.periodo !== periodo);

    if (isTopLevelInvalid) {
      return {
        toolbarStatusText: 'Información no disponible',
        isTotalError: true,
        kpisState: 'error' as const,
        kpis: buildErrorKpis(),
        donut: { state: 'error', total: 0, segments: [] } satisfies StatusDonutViewModel,
        deadlines: {
          state: 'error',
          verTodasHref: '/admin/solicitudes?dias=1-7',
          rows: [],
        } satisfies UpcomingDeadlinesViewModel,
        areaChart: {
          state: 'error',
          bars: [],
          scaleMax: 4,
          guideTicks: [1, 2, 3, 4],
        } satisfies AreaBarChartViewModel,
        recentActivity: {
          state: 'error',
          items: [],
        } satisfies RecentActivityViewModel,
      };
    }

    const verTodasHref = buildVerTodasHref(periodo, data.fechaInicio, data.fechaFin);

    // 1. Validación de Indicadores y Donut
    const validInd = validateIndicadores(data);
    const kpisState: 'error' | 'ready' = validInd ? 'ready' : 'error';
    const kpis = validInd ? buildReadyKpis(validInd) : buildErrorKpis();

    const donut: StatusDonutViewModel = !validInd
      ? { state: 'error', total: 0, segments: [] }
      : validInd.total === 0
        ? { state: 'empty', total: 0, segments: [] }
        : {
            state: 'ready',
            total: validInd.total,
            segments: [
              {
                id: 'en-proceso',
                label: 'En proceso',
                value: validInd.enProceso,
                percentageText: formatShareText(validInd.enProceso, validInd.total),
                color: '#F97316',
              },
              {
                id: 'entregadas',
                label: 'Entregadas',
                value: validInd.entregadas,
                percentageText: formatShareText(validInd.entregadas, validInd.total),
                color: '#22C55E',
              },
              {
                id: 'vencidas',
                label: 'Vencidas',
                value: validInd.vencidas,
                percentageText: formatShareText(validInd.vencidas, validInd.total),
                color: '#EF4444',
              },
            ],
          };

    // 2. Validación de Barras por área (porArea)
    let areaChart: AreaBarChartViewModel;
    const rawAreas = data.porArea;

    const areasItemsValid =
      data.disponibilidad?.porArea !== false &&
      Array.isArray(rawAreas) &&
      rawAreas.every(
        (item) =>
          item != null &&
          typeof item === 'object' &&
          typeof item.area === 'string' &&
          item.area.trim().length > 0 &&
          isNonNegativeInt(item.cantidad),
      );

    if (!areasItemsValid) {
      areaChart = { state: 'error', bars: [], scaleMax: 4, guideTicks: [1, 2, 3, 4] };
    } else {
      const sumAreas = rawAreas.reduce((acc, item) => acc + item.cantidad, 0);
      if (validInd !== null && sumAreas !== validInd.total) {
        areaChart = { state: 'error', bars: [], scaleMax: 4, guideTicks: [1, 2, 3, 4] };
      } else {
        const groupedBars = normalizeAndGroupAreas(rawAreas);
        if (sumAreas === 0 || groupedBars.length === 0) {
          areaChart = { state: 'empty', bars: [], scaleMax: 4, guideTicks: [1, 2, 3, 4] };
        } else {
          const maxBar = Math.max(...groupedBars.map((b) => b.cantidad));
          const { scaleMax, guideTicks } = buildScale(maxBar);
          areaChart = {
            state: 'ready',
            bars: groupedBars,
            scaleMax,
            guideTicks,
          };
        }
      }
    }

    // 3. Validación de Próximos vencimientos
    let deadlines: UpcomingDeadlinesViewModel;
    const rawDeadlines = data.proximosVencimientos;
    const deadlinesItemsValid =
      data.disponibilidad?.proximosVencimientos !== false &&
      Array.isArray(rawDeadlines) &&
      rawDeadlines.length <= 5 &&
      rawDeadlines.every(
        (item) =>
          item != null &&
          typeof item === 'object' &&
          typeof item.folio === 'string' &&
          item.folio.trim().length > 0 &&
          typeof item.asunto === 'string' &&
          item.asunto.trim().length > 0 &&
          isValidIsoDateOnly(item.fechaVencimiento) &&
          typeof item.diasRestantes === 'number' &&
          Number.isInteger(item.diasRestantes) &&
          item.diasRestantes >= 1 &&
          item.diasRestantes <= 7,
      );

    const deadlinesConsistentWithIndicadores =
      validInd === null
        ? true
        : validInd.proximasAVencer === 0
          ? (rawDeadlines?.length ?? 0) === 0
          : (rawDeadlines?.length ?? 0) >= 1 &&
            (rawDeadlines?.length ?? 0) <= Math.min(5, validInd.proximasAVencer);

    if (!deadlinesItemsValid || !deadlinesConsistentWithIndicadores) {
      deadlines = { state: 'error', verTodasHref, rows: [] };
    } else if (rawDeadlines.length === 0) {
      deadlines = { state: 'empty', verTodasHref, rows: [] };
    } else {
      deadlines = {
        state: 'ready',
        verTodasHref,
        rows: rawDeadlines.map((item) => {
          const folioTrim = item.folio.trim();
          return {
            folio: folioTrim,
            asunto: item.asunto.trim(),
            fechaVencimientoTexto: formatIsoDateToDdMmYyyy(item.fechaVencimiento),
            diasRestantes: item.diasRestantes,
            diasRestantesTexto:
              item.diasRestantes === 1 ? '1 día' : `${item.diasRestantes} días`,
            urgencia: item.diasRestantes <= 3 ? 'warning' : 'safe',
            detalleHref: `/admin/solicitudes/${encodeURIComponent(folioTrim)}`,
          };
        }),
      };
    }

    // 4. Validación de Actividad reciente
    let recentActivity: RecentActivityViewModel;
    const rawActivity = data.actividadReciente;
    const activityItemsValid =
      data.disponibilidad?.actividadReciente !== false &&
      Array.isArray(rawActivity) &&
      rawActivity.every(
        (item) =>
          item != null &&
          typeof item === 'object' &&
          typeof item.descripcion === 'string' &&
          item.descripcion.trim().length > 0 &&
          typeof item.folio === 'string' &&
          item.folio.trim().length > 0 &&
          typeof item.ocurridoEnUtc === 'string' &&
          !Number.isNaN(new Date(item.ocurridoEnUtc).getTime()),
      );

    if (!activityItemsValid) {
      recentActivity = { state: 'error', items: [] };
    } else if (rawActivity.length === 0) {
      recentActivity = { state: 'empty', items: [] };
    } else {
      recentActivity = {
        state: 'ready',
        items: rawActivity.slice(0, 5).map((item, idx) => ({
          id: `${item.folio.trim()}-${item.ocurridoEnUtc}-${idx}`,
          tipo: typeof item.tipo === 'string' ? item.tipo : 'Solicitud',
          descripcion: item.descripcion.trim(),
          folio: item.folio.trim(),
          tiempoRelativo: formatTiempoRelativo(item.ocurridoEnUtc, data.actualizadoEnUtc),
        })),
      };
    }

    const allWidgetsUnavailable =
      kpisState === 'error' &&
      donut.state === 'error' &&
      deadlines.state === 'error' &&
      areaChart.state === 'error' &&
      recentActivity.state === 'error';

    // Si hay recarga en vuelo tras error parcial, los bloques fallidos pasan a skeleton (loading) de inmediato
    const effectiveKpisState =
      kpisState === 'error' && query.isFetching ? ('loading' as const) : kpisState;
    const effectiveKpis =
      kpisState === 'error' && query.isFetching ? buildLoadingKpis() : kpis;
    const effectiveDonut: StatusDonutViewModel =
      donut.state === 'error' && query.isFetching
        ? { ...donut, state: 'loading' }
        : donut;
    const effectiveDeadlines: UpcomingDeadlinesViewModel =
      deadlines.state === 'error' && query.isFetching
        ? { ...deadlines, state: 'loading' }
        : deadlines;
    const effectiveAreaChart: AreaBarChartViewModel =
      areaChart.state === 'error' && query.isFetching
        ? { ...areaChart, state: 'loading' }
        : areaChart;
    const effectiveRecentActivity: RecentActivityViewModel =
      recentActivity.state === 'error' && query.isFetching
        ? { ...recentActivity, state: 'loading' }
        : recentActivity;

    const formattedUpdated = formatActualizadoTexto(data.actualizadoEnUtc);
    const toolbarStatusText = query.isFetching
      ? 'Actualizando…'
      : allWidgetsUnavailable || !formattedUpdated
        ? 'Información no disponible'
        : formattedUpdated;

    return {
      toolbarStatusText,
      isTotalError: !query.isFetching && allWidgetsUnavailable,
      kpisState: effectiveKpisState,
      kpis: effectiveKpis,
      donut: effectiveDonut,
      deadlines: effectiveDeadlines,
      areaChart: effectiveAreaChart,
      recentActivity: effectiveRecentActivity,
    };
  }, [periodo, query.data, query.isError, query.isFetching, query.isPending]);

  return {
    periodo,
    periodoOptions: PERIODO_OPTIONS,
    isFetching: query.isFetching,
    ...viewState,
    onPeriodoChange: handlePeriodoChange,
    refresh,
    stayOnDashboard,
    goToSolicitudes,
    selectDeadlineFolio,
  };
}
