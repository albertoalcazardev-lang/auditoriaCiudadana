import type { DiasRestantesTone } from '../types/solicitudes.types';

export const MEXICO_CITY_TIMEZONE = 'America/Mexico_City';
const MS_PER_DAY = 86_400_000;

export interface CalendarDateParts {
  year: number;
  month: number;
  day: number;
}

export interface MexicoCityNowParts extends CalendarDateParts {
  fechaFormatted: string;
  horaFormatted: string;
}

const mexicoCityFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: MEXICO_CITY_TIMEZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

export function getMexicoCityNowParts(now: Date = new Date()): MexicoCityNowParts {
  const parts = mexicoCityFormatter.formatToParts(now);
  const lookup = Object.fromEntries(parts.map((part) => [part.type, part.value]));

  const dayStr = lookup.day ?? '01';
  const monthStr = lookup.month ?? '01';
  const yearStr = lookup.year ?? '2026';
  const rawHour = lookup.hour ?? '00';
  const hourStr = rawHour === '24' ? '00' : rawHour;
  const minuteStr = lookup.minute ?? '00';

  return {
    year: Number(yearStr),
    month: Number(monthStr),
    day: Number(dayStr),
    fechaFormatted: `${dayStr}/${monthStr}/${yearStr}`,
    horaFormatted: `${hourStr}:${minuteStr}`,
  };
}

export function parseDdMmYyyy(value: string): CalendarDateParts | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value.trim());
  if (!match) return null;

  const day = Number(match[1]);
  const month = Number(match[2]);
  const year = Number(match[3]);

  if (year < 1900 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31) {
    return null;
  }

  const candidate = new Date(Date.UTC(year, month - 1, day));
  if (
    candidate.getUTCFullYear() !== year ||
    candidate.getUTCMonth() !== month - 1 ||
    candidate.getUTCDate() !== day
  ) {
    return null;
  }

  return { year, month, day };
}

export function toIsoDateString(parts: CalendarDateParts): string {
  return `${String(parts.year).padStart(4, '0')}-${String(parts.month).padStart(2, '0')}-${String(parts.day).padStart(2, '0')}`;
}

export function formatIsoToDdMmYyyy(isoDate: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(isoDate.trim());
  if (!match) return isoDate;
  return `${match[3]}/${match[2]}/${match[1]}`;
}

export function isValidTime24h(value: string): boolean {
  return /^([01]\d|2[0-3]):([0-5]\d)$/.test(value.trim());
}

export function compareCalendarDates(a: CalendarDateParts, b: CalendarDateParts): number {
  return Date.UTC(a.year, a.month - 1, a.day) - Date.UTC(b.year, b.month - 1, b.day);
}

export function computeDiasRestantesMexicoCity(
  fechaVencimientoText: string,
  now: Date = new Date(),
): number | null {
  const vencimiento = parseDdMmYyyy(fechaVencimientoText);
  if (!vencimiento) return null;

  const today = getMexicoCityNowParts(now);
  const diffMs =
    Date.UTC(vencimiento.year, vencimiento.month - 1, vencimiento.day) -
    Date.UTC(today.year, today.month - 1, today.day);

  return Math.max(0, Math.round(diffMs / MS_PER_DAY));
}

export function getDiasRestantesTone(dias: number | null): DiasRestantesTone {
  if (dias === null) return 'empty';
  if (dias === 0) return 'urgent';
  if (dias <= 7) return 'warning';
  return 'normal';
}
