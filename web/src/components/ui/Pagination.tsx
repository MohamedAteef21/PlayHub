import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  pageSizeOptions?: number[];
}

export function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50],
}: PaginationProps) {
  const { t } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  if (totalCount === 0) return null;

  return (
    <div className="mt-4 flex flex-wrap items-center justify-between gap-3 animate-fade-in">
      <p className="text-xs text-muted">
        {t('common.showingRange', { from, to, total: totalCount })}
      </p>
      <div className="flex flex-wrap items-center gap-2">
        {onPageSizeChange && (
          <label className="flex items-center gap-2 text-xs text-muted">
            <span>{t('common.pageSize')}</span>
            <select
              className="rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm text-text"
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
            >
              {pageSizeOptions.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        )}
        <Button
          type="button"
          variant="secondary"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          {t('common.prev')}
        </Button>
        <span className="min-w-[4.5rem] text-center text-sm text-muted">
          {page} / {totalPages}
        </span>
        <Button
          type="button"
          variant="secondary"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          {t('common.next')}
        </Button>
      </div>
    </div>
  );
}
