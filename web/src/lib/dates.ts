import { parseServerUtc } from '@/hooks/useSessions';

export const EGYPT_TZ = 'Africa/Cairo';

export function startOfMonth(): string {
  const d = new Date(todayEgypt() + 'T12:00:00');
  return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
}

/** Today's calendar date in Egypt (YYYY-MM-DD). */
export function today(): string {
  return todayEgypt();
}

/** Today's date in Egypt (YYYY-MM-DD). */
export function todayEgypt(): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: EGYPT_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}

export function toIsoDate(dateStr: string): string {
  return new Date(dateStr + 'T00:00:00').toISOString();
}

export function toIsoDateEnd(dateStr: string): string {
  return new Date(dateStr + 'T23:59:59').toISOString();
}

/**
 * Absolute datetime for display: Egypt timezone, 12-hour clock, with date.
 */
export function formatDateTimeEgypt(
  iso: string | null | undefined,
  locale: string = 'ar-EG'
): string {
  if (!iso) return '—';
  const ms = parseServerUtc(iso);
  return new Intl.DateTimeFormat(locale, {
    timeZone: EGYPT_TZ,
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(ms));
}

/** Time only in Egypt, 12-hour. */
export function formatTimeEgypt(
  iso: string | null | undefined,
  locale: string = 'ar-EG'
): string {
  if (!iso) return '—';
  const ms = parseServerUtc(iso);
  return new Intl.DateTimeFormat(locale, {
    timeZone: EGYPT_TZ,
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(ms));
}

/** Live Egypt clock string (date + 12h time), updates each call. */
export function formatNowEgypt(locale: string = 'ar-EG'): string {
  return new Intl.DateTimeFormat(locale, {
    timeZone: EGYPT_TZ,
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit',
    hour12: true,
  }).format(new Date());
}

/** Value for `<input type="datetime-local">` in Egypt wall time (YYYY-MM-DDTHH:mm). */
export function toEgyptDateTimeLocalInput(ms: number = Date.now()): string {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: EGYPT_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(new Date(ms));
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? '00';
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
}

/**
 * Interpret a datetime-local string as Africa/Cairo wall time and return UTC ISO.
 */
export function egyptLocalInputToUtcIso(localValue: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(localValue.trim());
  if (!m) throw new Error('Invalid datetime');
  const year = Number(m[1]);
  const month = Number(m[2]);
  const day = Number(m[3]);
  const hour = Number(m[4]);
  const minute = Number(m[5]);

  let utcMs = Date.UTC(year, month - 1, day, hour, minute, 0);
  for (let i = 0; i < 3; i++) {
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: EGYPT_TZ,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).formatToParts(new Date(utcMs));
    const get = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? '0');
    const asShown = Date.UTC(get('year'), get('month') - 1, get('day'), get('hour'), get('minute'));
    const desired = Date.UTC(year, month - 1, day, hour, minute);
    utcMs += desired - asShown;
  }
  return new Date(utcMs).toISOString();
}
