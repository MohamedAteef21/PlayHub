import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reportsApi } from '@/api/client';
import { formatCurrency, parseServerUtc } from '@/hooks/useSessions';
import { startOfMonth, today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, DateRangeBar, PageHeader, StatCard } from '@/components/ui/PageHelpers';

type ReportTab = 'cashDrawer' | 'revenue' | 'bestSellers' | 'devices';

export function ReportsPage() {
  const { t } = useTranslation();
  const [from, setFrom] = useState(startOfMonth());
  const [to, setTo] = useState(today());
  const [tab, setTab] = useState<ReportTab>('cashDrawer');
  const [cashDate, setCashDate] = useState(today());
  const [collectOpen, setCollectOpen] = useState(false);
  const [collectAmount, setCollectAmount] = useState('');
  const [collectNote, setCollectNote] = useState('');
  const [collectError, setCollectError] = useState('');
  const [collectMsg, setCollectMsg] = useState('');
  const queryClient = useQueryClient();

  const isoFrom = toIsoDate(from);
  const isoTo = toIsoDateEnd(to);

  const { data: drawer, isLoading: cdLoading } = useQuery({
    queryKey: ['reports-cash-drawer', cashDate],
    queryFn: () => reportsApi.getCashDrawer(cashDate),
    enabled: tab === 'cashDrawer',
    refetchInterval: 60_000,
  });

  const collectMutation = useMutation({
    mutationFn: () =>
      reportsApi.collectCash({
        amount: Number(collectAmount),
        note: collectNote.trim() || undefined,
        date: cashDate,
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(['reports-cash-drawer', cashDate], updated);
      void queryClient.invalidateQueries({ queryKey: ['reports-cash-drawer'] });
      setCollectOpen(false);
      setCollectMsg(t('reports.collectSuccess'));
    },
    onError: (e: Error) => setCollectError(e.message),
  });

  function openCollect() {
    setCollectAmount(drawer && drawer.drawerBalance > 0 ? String(drawer.drawerBalance) : '');
    setCollectNote('');
    setCollectError('');
    setCollectMsg('');
    setCollectOpen(true);
  }

  const { data: revenue, isLoading: revLoading } = useQuery({
    queryKey: ['reports-revenue', from, to],
    queryFn: () => reportsApi.getRevenue(isoFrom, isoTo),
    enabled: tab === 'revenue',
  });

  const { data: bestSellers = [], isLoading: bsLoading } = useQuery({
    queryKey: ['reports-best-sellers', from, to],
    queryFn: () => reportsApi.getBestSellers(isoFrom, isoTo),
    enabled: tab === 'bestSellers',
  });

  const { data: deviceUsage = [], isLoading: duLoading } = useQuery({
    queryKey: ['reports-device-usage', from, to],
    queryFn: () => reportsApi.getDeviceUsage(isoFrom, isoTo),
    enabled: tab === 'devices',
  });

  const tabs: { id: ReportTab; label: string }[] = [
    { id: 'cashDrawer', label: t('reports.cashDrawer') },
    { id: 'revenue', label: t('reports.revenue') },
    { id: 'bestSellers', label: t('reports.bestSellers') },
    { id: 'devices', label: t('reports.deviceUsage') },
  ];

  return (
    <div>
      <PageHeader title={t('reports.title')}>
        {tab === 'cashDrawer' ? (
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-muted">{t('reports.cashDay')}</span>
            <input
              type="date"
              value={cashDate}
              onChange={(e) => setCashDate(e.target.value)}
              className="rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm text-text"
            />
          </label>
        ) : (
          <DateRangeBar from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
        )}
      </PageHeader>

      <div className="mb-6 flex flex-wrap gap-2">
        {tabs.map(({ id, label }) => (
          <button
            key={id}
            onClick={() => setTab(id)}
            className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${tab === id ? 'bg-primary/15 text-primary' : 'text-muted hover:bg-surface-hover'}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'cashDrawer' && (
        cdLoading ? (
          <p className="text-muted">{t('common.loading')}</p>
        ) : drawer ? (
          <div className="space-y-6">
            <p className="max-w-3xl text-sm text-muted">{t('reports.cashDrawerHint')}</p>

            {collectMsg && (
              <p className="rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-sm text-success">
                {collectMsg}
              </p>
            )}

            <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-primary/35 bg-primary/10 p-4">
              <div>
                <p className="text-sm text-muted">{t('reports.drawerBalance')}</p>
                <p className="mt-1 text-3xl font-bold text-primary">{formatCurrency(drawer.drawerBalance)}</p>
                {drawer.drawerBalance <= 0 && (
                  <p className="mt-1 text-sm text-success">{t('reports.drawerEmpty')}</p>
                )}
              </div>
              <Button onClick={openCollect} disabled={drawer.drawerBalance <= 0}>
                {t('reports.collect')}
              </Button>
            </div>

            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard label={t('reports.collectedOnDay')} value={formatCurrency(drawer.collectedOnDay)} accent="success" />
              <StatCard label={t('reports.netCash')} value={formatCurrency(drawer.netCash)} />
              <StatCard label={t('reports.totalCashIn')} value={formatCurrency(drawer.totalCashIn)} />
              <StatCard label={t('reports.cashExpenses')} value={formatCurrency(drawer.cashExpenses)} accent="danger" />
            </div>

            <div>
              <h2 className="mb-3 text-sm font-semibold text-muted">{t('reports.cashIn')}</h2>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <StatCard label={t('reports.cashSessions')} value={formatCurrency(drawer.cashSessions)} />
                <StatCard label={t('reports.cashCafeteria')} value={formatCurrency(drawer.cashCafeteria)} />
                <StatCard label={t('reports.cashWalletTopUps')} value={formatCurrency(drawer.cashWalletTopUps)} />
                <StatCard label={t('reports.cashCollectedDebts')} value={formatCurrency(drawer.cashCollectedDebts)} />
                <StatCard label={t('reports.cashManualIn')} value={formatCurrency(drawer.cashManualIn)} accent="success" />
              </div>
            </div>

            <div>
              <h2 className="mb-3 text-sm font-semibold text-muted">{t('reports.otherChannels')}</h2>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <StatCard label={t('reports.bankTransferIn')} value={formatCurrency(drawer.bankTransferIn)} />
                <StatCard label={t('reports.digitalWalletIn')} value={formatCurrency(drawer.digitalWalletIn)} />
                <StatCard label={t('reports.paidFromCustomerWallets')} value={formatCurrency(drawer.paidFromCustomerWallets)} />
                <StatCard label={t('reports.newDeferredDebts')} value={formatCurrency(drawer.newDeferredDebts)} accent="danger" />
              </div>
            </div>

            {drawer.dayCollections.length > 0 && (
              <div>
                <h2 className="mb-3 text-sm font-semibold text-muted">{t('reports.dayCollections')}</h2>
                <DataTable headers={[t('activity.time'), t('reports.collectedBy'), t('reports.collectAmount'), t('reports.collectNote')]}>
                  {drawer.dayCollections.map((c) => (
                    <tr key={c.id} className="hover:bg-surface-hover">
                      <td className="px-4 py-3 text-sm text-muted">
                        {new Date(parseServerUtc(c.collectedAt)).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </td>
                      <td className="px-4 py-3">{c.collectedByName}</td>
                      <td className="px-4 py-3 font-medium text-success">{formatCurrency(c.amount)}</td>
                      <td className="px-4 py-3 text-sm text-muted">{c.note ?? '—'}</td>
                    </tr>
                  ))}
                </DataTable>
              </div>
            )}
          </div>
        ) : null
      )}

      <Modal open={collectOpen} onClose={() => setCollectOpen(false)} title={t('reports.collectTitle')}>
        <div className="space-y-3">
          <p className="text-sm text-muted">{t('reports.collectHint')}</p>
          <p className="text-sm">
            {t('reports.drawerBalance')}:{' '}
            <span className="font-bold text-primary">{formatCurrency(drawer?.drawerBalance ?? 0)}</span>
          </p>
          <div className="flex items-end gap-2">
            <div className="flex-1">
              <Input
                label={t('reports.collectAmount')}
                type="number"
                min={0}
                step="0.01"
                value={collectAmount}
                onChange={(e) => setCollectAmount(e.target.value)}
              />
            </div>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setCollectAmount(String(drawer?.drawerBalance ?? 0))}
            >
              {t('reports.collectAll')}
            </Button>
          </div>
          <Input
            label={t('reports.collectNote')}
            value={collectNote}
            onChange={(e) => setCollectNote(e.target.value)}
          />
          {collectError && <p className="text-sm text-danger">{collectError}</p>}
          <Button
            className="w-full"
            loading={collectMutation.isPending}
            disabled={
              !collectAmount ||
              Number(collectAmount) <= 0 ||
              Number(collectAmount) > (drawer?.drawerBalance ?? 0)
            }
            onClick={() => collectMutation.mutate()}
          >
            {t('reports.collect')}
          </Button>
        </div>
      </Modal>

      {tab === 'revenue' && (
        revLoading ? (
          <p className="text-muted">{t('common.loading')}</p>
        ) : revenue ? (
          <div className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard label={t('reports.totalRevenue')} value={formatCurrency(revenue.totalRevenue)} accent="success" />
              <StatCard label={t('reports.sessionRevenue')} value={formatCurrency(revenue.sessionRevenue)} />
              <StatCard label={t('reports.cafeteriaRevenue')} value={formatCurrency(revenue.cafeteriaRevenue)} />
              <StatCard label={t('reports.manualRevenue')} value={formatCurrency(revenue.manualRevenue ?? 0)} />
            </div>
            {revenue.daily.length > 0 && (
              <DataTable
                headers={[
                  t('accounting.date'),
                  t('reports.sessionRevenue'),
                  t('reports.cafeteriaRevenue'),
                  t('reports.manualRevenue'),
                  t('reports.totalRevenue'),
                ]}
              >
                {revenue.daily.map((d) => (
                  <tr key={d.date} className="hover:bg-surface-hover">
                    <td className="px-4 py-3">{d.date}</td>
                    <td className="px-4 py-3">{formatCurrency(d.sessionRevenue)}</td>
                    <td className="px-4 py-3">{formatCurrency(d.cafeteriaRevenue)}</td>
                    <td className="px-4 py-3">{formatCurrency(d.manualRevenue ?? 0)}</td>
                    <td className="px-4 py-3 font-medium text-success">{formatCurrency(d.total)}</td>
                  </tr>
                ))}
              </DataTable>
            )}
          </div>
        ) : null
      )}

      {tab === 'bestSellers' && (
        bsLoading ? (
          <p className="text-muted">{t('common.loading')}</p>
        ) : bestSellers.length === 0 ? (
          <p className="text-muted">{t('reports.noData')}</p>
        ) : (
          <DataTable headers={[t('inventory.item'), t('reports.quantity'), t('reports.revenue')]}>
            {bestSellers.map((item) => (
              <tr key={item.itemId} className="hover:bg-surface-hover">
                <td className="px-4 py-3">{item.itemName}</td>
                <td className="px-4 py-3">{item.totalQuantity}</td>
                <td className="px-4 py-3 font-medium text-success">{formatCurrency(item.totalRevenue)}</td>
              </tr>
            ))}
          </DataTable>
        )
      )}

      {tab === 'devices' && (
        duLoading ? (
          <p className="text-muted">{t('common.loading')}</p>
        ) : deviceUsage.length === 0 ? (
          <p className="text-muted">{t('reports.noData')}</p>
        ) : (
          <DataTable headers={[t('reports.device'), t('reports.room'), t('reports.hours'), t('reports.sessions')]}>
            {deviceUsage.map((d) => (
              <tr key={d.deviceId} className="hover:bg-surface-hover">
                <td className="px-4 py-3">{d.deviceName}</td>
                <td className="px-4 py-3">{d.roomName}</td>
                <td className="px-4 py-3">{d.totalHours.toFixed(1)}h</td>
                <td className="px-4 py-3">{d.sessionCount}</td>
              </tr>
            ))}
          </DataTable>
        )
      )}
    </div>
  );
}
