export type PeriodoDashboard =
  | 'este-mes'
  | 'mes-anterior'
  | 'ultimos-3-meses'
  | 'este-anio'
  | 'todo';

export interface PeriodoOption {
  value: PeriodoDashboard;
  label: string;
}

export const PERIODO_OPTIONS: readonly PeriodoOption[] = [
  { value: 'este-mes', label: 'Este mes' },
  { value: 'mes-anterior', label: 'Mes anterior' },
  { value: 'ultimos-3-meses', label: 'Últimos 3 meses' },
  { value: 'este-anio', label: 'Este año' },
  { value: 'todo', label: 'Todo' },
] as const;

export interface DashboardDisponibilidad {
  indicadores: boolean;
  porArea: boolean;
  proximosVencimientos: boolean;
  actividadReciente: boolean;
}

export interface DashboardIndicadores {
  total: number;
  enProceso: number;
  entregadas: number;
  vencidas: number;
  proximasAVencer: number;
  variacionTotalPorcentaje?: number | null;
}

export interface DashboardAreaItem {
  area: string;
  cantidad: number;
}

export interface DashboardProximoVencimientoItem {
  folio: string;
  asunto: string;
  fechaVencimiento: string; // yyyy-MM-dd
  diasRestantes: number; // 1..7
}

export interface DashboardActividadItem {
  tipo: string; // ej. "NuevaSolicitud"
  descripcion: string; // ej. "Nueva solicitud registrada"
  folio: string;
  ocurridoEnUtc: string; // ISO UTC
}

export interface DashboardResponse {
  periodo: PeriodoDashboard;
  fechaReferencia: string; // yyyy-MM-dd
  fechaInicio?: string | null; // yyyy-MM-dd (null en 'todo')
  fechaFin?: string | null; // yyyy-MM-dd (null en 'todo')
  actualizadoEnUtc: string; // ISO UTC
  disponibilidad?: Partial<DashboardDisponibilidad>;
  indicadores: DashboardIndicadores | null;
  porArea: DashboardAreaItem[] | null;
  proximosVencimientos: DashboardProximoVencimientoItem[] | null;
  actividadReciente: DashboardActividadItem[] | null;
}

export type WidgetState = 'loading' | 'error' | 'empty' | 'ready';

export type KpiCardId =
  | 'total'
  | 'en-proceso'
  | 'entregadas'
  | 'vencidas'
  | 'proximas-a-vencer';

export type KpiCardState = 'loading' | 'error' | 'ready';

export interface KpiCardViewModel {
  id: KpiCardId;
  label: string;
  state: KpiCardState;
  valueText: string;
  secondaryText: string | null;
  iconBg: string;
  iconColor: string;
}

export interface DonutSegmentViewModel {
  id: 'en-proceso' | 'entregadas' | 'vencidas';
  label: string;
  value: number;
  percentageText: string;
  color: string;
}

export interface StatusDonutViewModel {
  state: WidgetState;
  total: number;
  segments: DonutSegmentViewModel[];
}

export interface UpcomingDeadlineRowViewModel {
  folio: string;
  asunto: string;
  fechaVencimientoTexto: string; // dd/mm/aaaa
  diasRestantes: number;
  diasRestantesTexto: string; // "1 día" | "X días"
  urgencia: 'warning' | 'safe';
  detalleHref: string;
}

export interface UpcomingDeadlinesViewModel {
  state: WidgetState;
  verTodasHref: string;
  rows: UpcomingDeadlineRowViewModel[];
}

export interface AreaBarItemViewModel {
  area: string;
  cantidad: number;
  cantidadTexto: string;
}

export interface AreaBarChartViewModel {
  state: WidgetState;
  bars: AreaBarItemViewModel[];
  scaleMax: number;
  guideTicks: [number, number, number, number];
}

export interface RecentActivityItemViewModel {
  id: string;
  tipo: string;
  descripcion: string;
  folio: string;
  tiempoRelativo: string;
}

export interface RecentActivityViewModel {
  state: WidgetState;
  items: RecentActivityItemViewModel[];
}
