import { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { sessionsApi } from '@/api/client';
import { Badge } from '@/components/ui/Badge';
import { DataTable, DateRangeBar, PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';
import { formatCurrency } from '@/hooks/useSessions';
import { formatDateTimeEgypt, startOfMonth, today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { SessionMode, SessionStatus } from '@/types';

function statusBadge(status: number): 'gaming' | 'paused' | 'idle' {
  if (status === SessionStatus.Open) return 'gaming';
  if (status === SessionStatus.Paused) return 'paused';
  return 'idle';
}

export function SessionHistoryPage() {
  const { t, i18n } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const canView = hasPermission(user, Permissions.SessionsHistory);
  const [from, setFrom] = useState(startOfMonth());
  const [to, setTo] = useState(today());
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const { data, isLoading } = useQuery({
    queryKey: ['session-history', from, to, page, pageSize],
    queryFn: () => sessionsApi.getHistory(toIsoDate(from), toIsoDateEnd(to), page, pageSize),
    enabled: canView,
  });

  if (!canView) return <Navigate to="/" replace />;

  const locale = i18n.language === 'ar' ? 'ar-EG' : undefined;
  const rows = data?.items ?? [];

  function fmt(iso: string | null | undefined) {
    return formatDateTimeEgypt(iso, locale ?? 'ar-EG');
  }

  function changeRange(nextFrom: string, nextTo: string) {
    setFrom(nextFrom);
    setTo(nextTo);
    setPage(1);
  }

  return (
    <div>
      <PageHeader title={t('sessionHistory.title')}>
        <DateRangeBar
          from={from}
          to={to}
          onFromChange={(v) => changeRange(v, to)}
          onToChange={(v) => changeRange(from, v)}
        />
      </PageHeader>

      <p className="mb-4 text-sm text-muted">{t('sessionHistory.hint')}</p>

      {isLoading ? (
        <PageLoader />
      ) : rows.length === 0 ? (
        <p className="text-muted">{t('sessionHistory.empty')}</p>
      ) : (
        <>
          <DataTable
            headers={[
              t('session.device'),
              t('session.room'),
              t('session.mode'),
              t('common.status'),
              t('session.started'),
              t('session.openedBy'),
              t('session.closedAt'),
              t('session.closedBy'),
              t('session.total'),
            ]}
          >
            {rows.map((s) => (
              <tr key={s.id} className="hover:bg-surface-hover transition-colors">
                <td className="px-4 py-3 font-medium">{s.deviceName}</td>
                <td className="px-4 py-3">{s.roomName}</td>
                <td className="px-4 py-3">
                  {s.sessionMode === SessionMode.Gaming ? t('session.gaming') : t('session.watching')}
                </td>
                <td className="px-4 py-3">
                  <Badge status={statusBadge(s.status)}>
                    {s.status === SessionStatus.Open
                      ? t('dashboard.gaming')
                      : s.status === SessionStatus.Paused
                        ? t('dashboard.paused')
                        : t('sessionHistory.closed')}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-sm whitespace-nowrap">{fmt(s.startedAt)}</td>
                <td className="px-4 py-3">{s.openedByName}</td>
                <td className="px-4 py-3 text-sm whitespace-nowrap">{fmt(s.closedAt)}</td>
                <td className="px-4 py-3">{s.closedByName ?? '—'}</td>
                <td className="px-4 py-3 font-medium">
                  {s.status === SessionStatus.Closed ? formatCurrency(s.totalCost) : '—'}
                </td>
              </tr>
            ))}
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
