import type {
  ListarSolicitudesParams,
  SolicitudActiveChip,
  SolicitudDiasFilter,
  SolicitudPageSize,
  SolicitudSortDirection,
  SolicitudSortField,
  SolicitudesFilterDraft,
  SolicitudesFilterErrors,
  SolicitudesUrlQueryState,
} from '../types/solicitudes.types';
import {
  compareCalendarDates,
  formatIsoToDdMmYyyy,
  parseDdMmYyyy,
  toIsoDateString,
} from './solicitudesDateUtils';

const VALID_SORT_FIELDS: readonly SolicitudSortField[] = [
  'vencimiento',
  'folio',
  'entrega',
  'dias',
];

const VALID_DIAS_FILTERS: readonly SolicitudDiasFilter[] = [
  'todos',
  '0',
  '1-3',
  '4-7',
  '8+',
  '1-7',
  'custom',
];

const SEARCH_DATE_PATTERN = /^\d{2}\/\d{2}\/\d{4}$/;
const INTEGER_0_TO_999_PATTERN = /^\d{1,3}$/;

export const EMPTY_FILTER_DRAFT: SolicitudesFilterDraft = {
  entDesde: '',
  entHasta: '',
  venDesde: '',
  venHasta: '',
  dias: 'todos',
  diasMin: '',
  diasMax: '',
};

export function parseSolicitudesSearchParams(
  searchParams: URLSearchParams,
): SolicitudesUrlQueryState {
  const q = (searchParams.get('q') ?? '').trim();
  const ent_d = (searchParams.get('ent_d') ?? '').trim();
  const ent_h = (searchParams.get('ent_h') ?? '').trim();
  const ven_d = (searchParams.get('ven_d') ?? '').trim();
  const ven_h = (searchParams.get('ven_h') ?? '').trim();

  const rawDias = (searchParams.get('dias') ?? '').trim();
  const dias: SolicitudDiasFilter = (VALID_DIAS_FILTERS as readonly string[]).includes(rawDias)
    ? (rawDias as SolicitudDiasFilter)
    : 'todos';

  let dias_min: number | undefined;
  let dias_max: number | undefined;
  if (dias === 'custom') {
    const rawMin = (searchParams.get('dias_min') ?? '').trim();
    const rawMax = (searchParams.get('dias_max') ?? '').trim();
    if (INTEGER_0_TO_999_PATTERN.test(rawMin)) {
      dias_min = Number(rawMin);
    }
    if (INTEGER_0_TO_999_PATTERN.test(rawMax)) {
      dias_max = Number(rawMax);
    }
  }

  const rawSort = (searchParams.get('sort') ?? '').trim();
  const sort: SolicitudSortField = (VALID_SORT_FIELDS as readonly string[]).includes(rawSort)
    ? (rawSort as SolicitudSortField)
    : 'vencimiento';

  const rawDir = (searchParams.get('dir') ?? '').trim().toLowerCase();
  const dir: SolicitudSortDirection = rawDir === 'desc' ? 'desc' : 'asc';

  const rawPage = (searchParams.get('page') ?? '').trim();
  const page = /^[1-9]\d*$/.test(rawPage) ? Number(rawPage) : 1;

  const rawSize = (searchParams.get('size') ?? '').trim();
  const size: SolicitudPageSize = rawSize === '25' ? 25 : rawSize === '50' ? 50 : 10;

  return {
    q,
    ent_d,
    ent_h,
    ven_d,
    ven_h,
    dias,
    dias_min,
    dias_max,
    sort,
    dir,
    page,
    size,
  };
}

export function buildSolicitudesSearchParams(
  state: SolicitudesUrlQueryState,
): URLSearchParams {
  const params = new URLSearchParams();

  if (state.q.trim()) {
    params.set('q', state.q.trim());
  }
  if (state.ent_d.trim()) {
    params.set('ent_d', state.ent_d.trim());
  }
  if (state.ent_h.trim()) {
    params.set('ent_h', state.ent_h.trim());
  }
  if (state.ven_d.trim()) {
    params.set('ven_d', state.ven_d.trim());
  }
  if (state.ven_h.trim()) {
    params.set('ven_h', state.ven_h.trim());
  }
  if (state.dias !== 'todos') {
    params.set('dias', state.dias);
    if (state.dias === 'custom') {
      if (state.dias_min !== undefined) {
        params.set('dias_min', String(state.dias_min));
      }
      if (state.dias_max !== undefined) {
        params.set('dias_max', String(state.dias_max));
      }
    }
  }
  if (state.sort !== 'vencimiento' || state.dir !== 'asc') {
    params.set('sort', state.sort);
    params.set('dir', state.dir);
  }
  if (state.page > 1) {
    params.set('page', String(state.page));
  }
  if (state.size !== 10) {
    params.set('size', String(state.size));
  }

  return params;
}

export function toListarSolicitudesApiParams(
  state: SolicitudesUrlQueryState,
): ListarSolicitudesParams {
  return {
    q: state.q.trim() || undefined,
    ent_d: state.ent_d.trim() || undefined,
    ent_h: state.ent_h.trim() || undefined,
    ven_d: state.ven_d.trim() || undefined,
    ven_h: state.ven_h.trim() || undefined,
    dias: state.dias === 'todos' ? undefined : state.dias,
    dias_min: state.dias === 'custom' ? state.dias_min : undefined,
    dias_max: state.dias === 'custom' ? state.dias_max : undefined,
    sort: state.sort,
    dir: state.dir,
    page: state.page,
    size: state.size,
  };
}

export function isImpossibleCalendarDateQuery(q: string): boolean {
  const trimmed = q.trim();
  if (!SEARCH_DATE_PATTERN.test(trimmed)) return false;
  return parseDdMmYyyy(trimmed) === null;
}

export function buildFilterDraftFromState(
  state: SolicitudesUrlQueryState,
): SolicitudesFilterDraft {
  return {
    entDesde: state.ent_d ? formatIsoToDdMmYyyy(state.ent_d) : '',
    entHasta: state.ent_h ? formatIsoToDdMmYyyy(state.ent_h) : '',
    venDesde: state.ven_d ? formatIsoToDdMmYyyy(state.ven_d) : '',
    venHasta: state.ven_h ? formatIsoToDdMmYyyy(state.ven_h) : '',
    dias: state.dias,
    diasMin:
      state.dias === 'custom' && state.dias_min !== undefined ? String(state.dias_min) : '',
    diasMax:
      state.dias === 'custom' && state.dias_max !== undefined ? String(state.dias_max) : '',
  };
}

export function validateFilterDraft(draft: SolicitudesFilterDraft): {
  errors: SolicitudesFilterErrors;
  isValid: boolean;
  isoValues: {
    ent_d: string;
    ent_h: string;
    ven_d: string;
    ven_h: string;
    dias: SolicitudDiasFilter;
    dias_min?: number;
    dias_max?: number;
  };
} {
  const errors: SolicitudesFilterErrors = {};

  const entDesdeTrimmed = draft.entDesde.trim();
  const entHastaTrimmed = draft.entHasta.trim();
  const parsedEntDesde = entDesdeTrimmed ? parseDdMmYyyy(entDesdeTrimmed) : null;
  const parsedEntHasta = entHastaTrimmed ? parseDdMmYyyy(entHastaTrimmed) : null;

  if (entDesdeTrimmed && !parsedEntDesde) {
    errors.entDesde = 'Ingresa una fecha inicial válida (dd/mm/aaaa).';
  }
  if (entHastaTrimmed && !parsedEntHasta) {
    errors.entHasta = 'Ingresa una fecha final válida (dd/mm/aaaa).';
  }
  if (parsedEntDesde && parsedEntHasta && compareCalendarDates(parsedEntHasta, parsedEntDesde) < 0) {
    errors.entRange = 'La fecha final de entrega no puede ser anterior a la fecha inicial.';
  }

  const venDesdeTrimmed = draft.venDesde.trim();
  const venHastaTrimmed = draft.venHasta.trim();
  const parsedVenDesde = venDesdeTrimmed ? parseDdMmYyyy(venDesdeTrimmed) : null;
  const parsedVenHasta = venHastaTrimmed ? parseDdMmYyyy(venHastaTrimmed) : null;

  if (venDesdeTrimmed && !parsedVenDesde) {
    errors.venDesde = 'Ingresa una fecha inicial válida (dd/mm/aaaa).';
  }
  if (venHastaTrimmed && !parsedVenHasta) {
    errors.venHasta = 'Ingresa una fecha final válida (dd/mm/aaaa).';
  }
  if (parsedVenDesde && parsedVenHasta && compareCalendarDates(parsedVenHasta, parsedVenDesde) < 0) {
    errors.venRange = 'La fecha final de vencimiento no puede ser anterior a la fecha inicial.';
  }

  let dias_min: number | undefined;
  let dias_max: number | undefined;

  if (draft.dias === 'custom') {
    const minTrimmed = draft.diasMin.trim();
    const maxTrimmed = draft.diasMax.trim();

    if (!minTrimmed && !maxTrimmed) {
      errors.diasCustom = 'Ingresa al menos el mínimo o máximo de días (0 a 999).';
    } else if (minTrimmed && !INTEGER_0_TO_999_PATTERN.test(minTrimmed)) {
      errors.diasCustom = 'El mínimo debe ser un número entero entre 0 y 999.';
    } else if (maxTrimmed && !INTEGER_0_TO_999_PATTERN.test(maxTrimmed)) {
      errors.diasCustom = 'El máximo debe ser un número entero entre 0 y 999.';
    } else {
      const parsedMin = minTrimmed ? Number(minTrimmed) : undefined;
      const parsedMax = maxTrimmed ? Number(maxTrimmed) : undefined;
      if (parsedMin !== undefined && parsedMax !== undefined && parsedMin > parsedMax) {
        errors.diasCustom = 'El mínimo de días no puede ser mayor al máximo.';
      } else {
        dias_min = parsedMin;
        dias_max = parsedMax;
      }
    }
  }

  const isValid = Object.keys(errors).length === 0;

  return {
    errors,
    isValid,
    isoValues: {
      ent_d: parsedEntDesde ? toIsoDateString(parsedEntDesde) : '',
      ent_h: parsedEntHasta ? toIsoDateString(parsedEntHasta) : '',
      ven_d: parsedVenDesde ? toIsoDateString(parsedVenDesde) : '',
      ven_h: parsedVenHasta ? toIsoDateString(parsedVenHasta) : '',
      dias: draft.dias,
      dias_min: draft.dias === 'custom' ? dias_min : undefined,
      dias_max: draft.dias === 'custom' ? dias_max : undefined,
    },
  };
}

function formatDiasFilterLabel(state: SolicitudesUrlQueryState): string | null {
  switch (state.dias) {
    case '0':
      return 'Días restantes: Vencidas (0)';
    case '1-3':
      return 'Días restantes: 1–3 días';
    case '4-7':
      return 'Días restantes: 4–7 días';
    case '8+':
      return 'Días restantes: 8 o más';
    case '1-7':
      return 'Días restantes: 1–7 días';
    case 'custom': {
      const hasMin = state.dias_min !== undefined;
      const hasMax = state.dias_max !== undefined;
      if (hasMin && hasMax) {
        return `Días restantes: ${state.dias_min}–${state.dias_max} días`;
      }
      if (hasMin) {
        return `Días restantes: desde ${state.dias_min} días`;
      }
      if (hasMax) {
        return `Días restantes: hasta ${state.dias_max} días`;
      }
      return 'Días restantes: Personalizado';
    }
    default:
      return null;
  }
}

export function buildActiveFilterChips(
  state: SolicitudesUrlQueryState,
): SolicitudActiveChip[] {
  const chips: SolicitudActiveChip[] = [];

  if (state.ent_d || state.ent_h) {
    const d = state.ent_d ? formatIsoToDdMmYyyy(state.ent_d) : '';
    const h = state.ent_h ? formatIsoToDdMmYyyy(state.ent_h) : '';
    const label =
      d && h
        ? `Entrega: ${d} – ${h}`
        : d
          ? `Entrega: desde ${d}`
          : `Entrega: hasta ${h}`;
    chips.push({ key: 'entrega', label });
  }

  if (state.ven_d || state.ven_h) {
    const d = state.ven_d ? formatIsoToDdMmYyyy(state.ven_d) : '';
    const h = state.ven_h ? formatIsoToDdMmYyyy(state.ven_h) : '';
    const label =
      d && h
        ? `Vencimiento: ${d} – ${h}`
        : d
          ? `Vencimiento: desde ${d}`
          : `Vencimiento: hasta ${h}`;
    chips.push({ key: 'vencimiento', label });
  }

  const diasLabel = formatDiasFilterLabel(state);
  if (diasLabel) {
    chips.push({ key: 'dias', label: diasLabel });
  }

  return chips;
}

export function formatMedioEntregaLabel(medio: string): string {
  switch (medio) {
    case 'Digital':
      return 'Digital';
    case 'Impreso':
      return 'Impreso';
    case 'PortalWeb':
      return 'Portal web';
    case 'CorreoElectronico':
      return 'Correo electrónico';
    default:
      return medio;
  }
}

export function formatEstatusEfectivoLabel(estatus: string): string {
  switch (estatus) {
    case 'EnProceso':
      return 'En proceso';
    case 'Entregada':
      return 'Entregada';
    case 'Vencida':
      return 'Vencida';
    default:
      return estatus;
  }
}

export function getEstatusBadgeModifier(
  estatus: string,
): 'en-proceso' | 'entregada' | 'vencida' {
  if (estatus === 'Entregada') return 'entregada';
  if (estatus === 'Vencida') return 'vencida';
  return 'en-proceso';
}

export function getDiasChipModifier(dias: number): 'green' | 'orange' | 'red' {
  if (dias <= 0) return 'red';
  if (dias <= 7) return 'orange';
  return 'green';
}

export function buildPaginationRange(
  currentPage: number,
  totalPages: number,
): Array<number | 'ellipsis'> {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, idx) => idx + 1);
  }

  const pages: Array<number | 'ellipsis'> = [1];
  const start = Math.max(2, currentPage - 1);
  const end = Math.min(totalPages - 1, currentPage + 1);

  if (start > 2) {
    pages.push('ellipsis');
  }

  for (let p = start; p <= end; p += 1) {
    pages.push(p);
  }

  if (end < totalPages - 1) {
    pages.push('ellipsis');
  }

  pages.push(totalPages);
  return pages;
}
