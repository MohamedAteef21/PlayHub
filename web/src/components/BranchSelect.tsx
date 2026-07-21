import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/store';

/** Append " — BranchName" for clarity on listed data. */
export function withBranchName(label: string, branchName: string | null | undefined) {
  const name = (branchName ?? '').trim();
  if (!name) return label;
  return `${label} — ${name}`;
}

export function useBranchName() {
  const user = useAuthStore((s) => s.user);
  return (branchId: string | null | undefined) => {
    if (!branchId) return '';
    return user?.branches.find((b) => b.id === branchId)?.name ?? '';
  };
}

type BranchTargetSelectProps = {
  value: string;
  onChange: (branchId: string) => void;
  required?: boolean;
  className?: string;
};

/**
 * Master-only: pick which branch a new item belongs to.
 * Staff never see this — creates always use their active branch.
 */
export function BranchTargetSelect({ value, onChange, required = true, className }: BranchTargetSelectProps) {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);

  if (!user?.isMaster) return null;

  const branches = user.branches ?? [];

  return (
    <div className={className}>
      <label className="mb-1 block text-sm text-muted">
        {t('common.targetBranch')}
        {required ? ' *' : ''}
      </label>
      <select
        className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
        value={value}
        required={required}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="">{t('common.selectBranch')}</option>
        {branches.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
            {b.id === activeBranchId ? ` (${t('common.current')})` : ''}
          </option>
        ))}
      </select>
      <p className="mt-1 text-xs text-muted">{t('common.targetBranchHint')}</p>
    </div>
  );
}

/** Master dashboard / list scope: one branch or all. */
type BranchScopeSelectProps = {
  value: string; // '' | 'all' | branchId — use 'all' or specific id; empty means active
  onChange: (value: string) => void;
};

export function BranchScopeSelect({ value, onChange }: BranchScopeSelectProps) {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);

  if (!user?.isMaster) return null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="text-sm text-muted">{t('common.branchScope')}</label>
      <select
        className="rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="all">{t('common.allBranches')}</option>
        {(user.branches ?? []).map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </select>
    </div>
  );
}
