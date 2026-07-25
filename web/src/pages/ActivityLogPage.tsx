import { useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { auditApi, usersApi } from '@/api/client';
import { formatDateTimeEgypt } from '@/lib/dates';
import { useAuthStore } from '@/store';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { DataTable, PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

const selectClass = 'w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm';

/** Detail keys are technical (English) property names; show them as simple labels. */
function formatDetails(json: string): string {
  try {
    const obj = JSON.parse(json) as Record<string, unknown>;
    const parts: string[] = [];
    for (const [key, value] of Object.entries(obj)) {
      if (value === null || value === undefined || value === '') continue;
      if (typeof value === 'object') continue;
      parts.push(`${key}: ${String(value)}`);
    }
    return parts.join(' · ');
  } catch {
    return '';
  }
}

export function ActivityLogPage() {
  const { t, i18n } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const canView = !!user?.isMaster;

  const [userId, setUserId] = useState('');
  const [actionType, setActionType] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const query = useMemo(
    () => ({
      page,
      pageSize,
      userId: userId || undefined,
      actionType: actionType || undefined,
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
    }),
    [page, pageSize, userId, actionType, from, to]
  );

  const { data, isLoading } = useQuery({
    queryKey: ['audit-logs', query],
    queryFn: () => auditApi.getLogs(query),
    enabled: canView,
  });

  const { data: usersPage } = useQuery({
    queryKey: ['users', 1, 100],
    queryFn: () => usersApi.getAll(1, 100),
    enabled: canView,
  });
  const users = usersPage?.items ?? [];

  const logs = data?.items ?? [];

  const actionKeys = useMemo(() => {
    const ar = i18n.getResourceBundle('ar', 'translation') as {
      activity?: { actions?: Record<string, string> };
    };
    return Object.keys(ar?.activity?.actions ?? {});
  }, [i18n]);

  function actionLabel(code: string) {
    return t(`activity.actions.${code.replace(/\./g, '_')}`, { defaultValue: code });
  }

  const hasFilters = !!(userId || actionType || from || to);

  if (!canView) return <Navigate to="/" replace />;

  return (
    <div>
      <PageHeader title={t('activity.title')} />
      <p className="mb-4 max-w-2xl text-sm text-muted">{t('activity.hint')}</p>

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <div>
          <p className="mb-1 text-xs font-medium text-muted">{t('activity.user')}</p>
          <select
            className={selectClass}
            value={userId}
            onChange={(e) => {
              setUserId(e.target.value);
              setPage(1);
            }}
          >
            <option value="">{t('activity.allUsers')}</option>
            {users.map((u) => (
              <option key={u.id} value={u.id}>
                {u.firstName} {u.lastName} ({u.username})
              </option>
            ))}
          </select>
        </div>
        <div>
          <p className="mb-1 text-xs font-medium text-muted">{t('activity.action')}</p>
          <select
            className={selectClass}
            value={actionType}
            onChange={(e) => {
              setActionType(e.target.value);
              setPage(1);
            }}
          >
            <option value="">{t('activity.allActions')}</option>
            {actionKeys.map((key) => {
              const code = key.replace(/_/g, '.');
              return (
                <option key={key} value={code}>
                  {actionLabel(code)}
                </option>
              );
            })}
          </select>
        </div>
        <Input
          label={t('activity.from')}
          type="date"
          value={from}
          onChange={(e) => {
            setFrom(e.target.value);
            setPage(1);
          }}
        />
        <Input
          label={t('activity.to')}
          type="date"
          value={to}
          onChange={(e) => {
            setTo(e.target.value);
            setPage(1);
          }}
        />
        <div className="flex items-end">
          <Button
            variant="secondary"
            disabled={!hasFilters}
            onClick={() => {
              setUserId('');
              setActionType('');
              setFrom('');
              setTo('');
              setPage(1);
            }}
          >
            {t('activity.clearFilters')}
          </Button>
        </div>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : logs.length === 0 ? (
        <p className="rounded-lg border border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          {t('activity.noResults')}
        </p>
      ) : (
        <>
          <DataTable
            headers={[
              t('activity.time'),
              t('activity.user'),
              t('activity.action'),
              t('activity.branch'),
              t('activity.details'),
            ]}
          >
            {logs.map((log) => {
              const details = formatDetails(log.details);
              return (
                <tr key={log.id} className="hover:bg-surface-hover">
                  <td className="whitespace-nowrap px-4 py-3 text-sm text-muted" dir="ltr">
                    {formatDateTimeEgypt(log.timestamp)}
                  </td>
                  <td className="px-4 py-3 font-medium">{log.userName}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex items-center gap-2">
                      {actionLabel(log.actionType)}
                      {!log.success && <Badge status="paused">{t('activity.failed')}</Badge>}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-muted">{log.branchName ?? '—'}</td>
                  <td className="max-w-md px-4 py-3 text-xs text-muted" dir="ltr">
                    {details || '—'}
                  </td>
                </tr>
              );
            })}
          </DataTable>
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={data?.totalCount ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </>
      )}
    </div>
  );
}
