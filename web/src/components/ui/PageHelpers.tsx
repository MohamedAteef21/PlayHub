import { useTranslation } from 'react-i18next';

interface DateRangeBarProps {
  from: string;
  to: string;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
}

export function DateRangeBar({ from, to, onFromChange, onToChange }: DateRangeBarProps) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-muted">{t('common.from')}</span>
        <input
          type="date"
          value={from}
          onChange={(e) => onFromChange(e.target.value)}
          className="rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm text-text"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-muted">{t('common.to')}</span>
        <input
          type="date"
          value={to}
          onChange={(e) => onToChange(e.target.value)}
          className="rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm text-text"
        />
      </label>
    </div>
  );
}

export function PageHeader({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4 animate-fade-in">
      <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
      {children}
    </div>
  );
}

export function DataTable({ headers, children }: { headers: string[]; children: React.ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-border animate-fade-in">
      <table className="w-full min-w-[640px] text-sm">
        <thead>
          <tr className="border-b border-border bg-surface-elevated text-start text-muted">
            {headers.map((h) => (
              <th key={h} className="px-4 py-3 font-medium">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">{children}</tbody>
      </table>
    </div>
  );
}

export function StatCard({ label, value, accent }: { label: string; value: string; accent?: 'success' | 'danger' | 'primary' }) {
  const accentClass =
    accent === 'success' ? 'text-success' : accent === 'danger' ? 'text-danger' : accent === 'primary' ? 'text-primary' : 'text-text';
  return (
    <div className="rounded-xl border border-border bg-surface-elevated p-4 transition-all duration-300 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-lg hover:shadow-primary/5 animate-fade-in">
      <p className="text-sm text-muted">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${accentClass}`}>{value}</p>
    </div>
  );
}
