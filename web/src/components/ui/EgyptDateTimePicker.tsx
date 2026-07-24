import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

/** Stored value format: YYYY-MM-DDTHH:mm (24h Egypt wall clock). */

function pad(n: number) {
  return String(n).padStart(2, '0');
}

function parseLocal(value: string): { date: string; hour24: number; minute: number } | null {
  const m = /^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})/.exec(value.trim());
  if (!m) return null;
  return { date: m[1], hour24: Number(m[2]), minute: Number(m[3]) };
}

function buildLocal(date: string, hour24: number, minute: number) {
  const h = ((hour24 % 24) + 24) % 24;
  const min = ((minute % 60) + 60) % 60;
  return `${date}T${pad(h)}:${pad(min)}`;
}

function to12h(hour24: number): { hour12: number; isPm: boolean } {
  const isPm = hour24 >= 12;
  const hour12 = hour24 % 12 === 0 ? 12 : hour24 % 12;
  return { hour12, isPm };
}

function from12h(hour12: number, isPm: boolean): number {
  const h = Math.min(12, Math.max(1, hour12));
  if (h === 12) return isPm ? 12 : 0;
  return isPm ? h + 12 : h;
}

function Stepper({
  label,
  value,
  onBump,
  display,
}: {
  label: string;
  value: number;
  display?: string;
  onBump: (delta: number) => void;
}) {
  return (
    <div className="flex min-w-[4.5rem] flex-col items-center gap-1">
      <span className="text-[11px] text-muted">{label}</span>
      <button
        type="button"
        className="flex h-8 w-full items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover"
        onClick={() => onBump(1)}
        aria-label={`+${label}`}
      >
        ▲
      </button>
      <div className="flex h-10 w-full items-center justify-center rounded-md border border-primary/30 bg-primary/10 font-mono text-lg font-semibold tabular-nums">
        {display ?? pad(value)}
      </div>
      <button
        type="button"
        className="flex h-8 w-full items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover"
        onClick={() => onBump(-1)}
        aria-label={`-${label}`}
      >
        ▼
      </button>
    </div>
  );
}

export function EgyptDateTimePicker({
  label,
  value,
  onChange,
  optional,
  onClear,
  hint,
}: {
  label: string;
  value: string;
  onChange: (next: string) => void;
  optional?: boolean;
  onClear?: () => void;
  hint?: string;
}) {
  const { t } = useTranslation();
  const parsed = useMemo(() => parseLocal(value), [value]);
  const hour24 = parsed?.hour24 ?? 12;
  const minute = parsed?.minute ?? 0;
  const date = parsed?.date ?? value.slice(0, 10);
  const { hour12, isPm } = to12h(hour24);

  function setParts(next: { date?: string; hour24?: number; minute?: number }) {
    onChange(
      buildLocal(next.date ?? date, next.hour24 ?? hour24, next.minute ?? minute)
    );
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <label className="block text-sm font-medium text-muted">
          {label}
          {optional ? (
            <span className="ms-1 text-[11px] font-normal opacity-70">({t('common.optional')})</span>
          ) : null}
        </label>
        {optional && onClear && value && (
          <button type="button" className="text-[11px] font-semibold text-muted hover:text-text" onClick={onClear}>
            {t('dashboard.clearEndTime')}
          </button>
        )}
      </div>

      <input
        type="date"
        className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-text"
        value={date}
        onChange={(e) => {
          if (!e.target.value) return;
          setParts({ date: e.target.value });
        }}
      />

      <div className="flex flex-wrap items-end justify-center gap-2 rounded-xl border border-border bg-surface-elevated/50 p-3">
        <Stepper
          label={t('dashboard.hour')}
          value={hour12}
          display={String(hour12)}
          onBump={(d) => {
            let next12 = hour12 + d;
            let nextPm = isPm;
            if (next12 > 12) {
              next12 = 1;
              nextPm = !isPm;
            } else if (next12 < 1) {
              next12 = 12;
              nextPm = !isPm;
            }
            setParts({ hour24: from12h(next12, nextPm) });
          }}
        />
        <div className="pb-10 text-xl font-bold text-muted">:</div>
        <Stepper
          label={t('dashboard.minute')}
          value={minute}
          onBump={(d) => {
            let next = minute + d * 5;
            let h = hour24;
            if (next >= 60) {
              next = 0;
              h = (h + 1) % 24;
            } else if (next < 0) {
              next = 55;
              h = (h + 23) % 24;
            }
            setParts({ hour24: h, minute: next });
          }}
        />
        <div className="flex min-w-[4.75rem] flex-col items-center gap-1">
          <span className="text-[11px] text-muted">{t('dashboard.dayPart')}</span>
          <button
            type="button"
            className="flex h-8 w-full items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover"
            onClick={() => setParts({ hour24: from12h(hour12, !isPm) })}
            aria-label={t('dashboard.toggleAmPm')}
          >
            ▲
          </button>
          <button
            type="button"
            className={`flex h-10 w-full items-center justify-center rounded-md border text-sm font-bold ${
              isPm
                ? 'border-primary/40 bg-primary/15 text-primary'
                : 'border-accent/40 bg-accent/15 text-accent'
            }`}
            onClick={() => setParts({ hour24: from12h(hour12, !isPm) })}
          >
            {isPm ? t('dashboard.pm') : t('dashboard.am')}
          </button>
          <button
            type="button"
            className="flex h-8 w-full items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover"
            onClick={() => setParts({ hour24: from12h(hour12, !isPm) })}
            aria-label={t('dashboard.toggleAmPm')}
          >
            ▼
          </button>
        </div>
      </div>

      {hint && <p className="text-[11px] text-muted">{hint}</p>}
    </div>
  );
}
