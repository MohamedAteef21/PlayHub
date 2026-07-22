import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { accountingApi, assetsApi, authApi, cafeteriaApi, sessionsApi } from '@/api/client';
import { BranchScopeSelect, withBranchName } from '@/components/BranchSelect';
import { formatCurrency } from '@/hooks/useSessions';
import { today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { PageHeader, StatCard } from '@/components/ui/PageHelpers';
import { useState } from 'react';

export function HomeDashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const setAuth = useAuthStore((s) => s.setAuth);
  const canReports = hasPermission(user, Permissions.ReportsView);
  const [scopeBusy, setScopeBusy] = useState(false);

  const scopeValue = activeBranchId ?? 'all';

  async function handleScopeChange(value: string) {
    if (scopeBusy) return;
    const next = value === 'all' ? null : value;
    if (next === activeBranchId) return;
    setScopeBusy(true);
    try {
      const res = await authApi.selectBranch(next);
      setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      // Soft reload queries via full navigation keeps SignalR/token in sync.
      window.location.assign('/');
    } catch {
      setScopeBusy(false);
    }
  }

  const { data: sessions = [] } = useQuery({
    queryKey: ['sessions', 'active', user?.id, activeBranchId],
    queryFn: sessionsApi.getActive,
    refetchInterval: 10000,
    meta: { silent: true },
  });

  const { data: dashboard } = useQuery({
    queryKey: ['assets-dashboard', user?.id, activeBranchId],
    queryFn: assetsApi.getDashboard,
  });

  const { data: items = [] } = useQuery({
    queryKey: ['cafeteria-items', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getItems(),
  });

  const day = today();
  const { data: finance } = useQuery({
    queryKey: ['home-finance', day, user?.id, activeBranchId],
    queryFn: () =>
      accountingApi.getDashboard(
        toIsoDate(day),
        toIsoDateEnd(day),
        activeBranchId ?? undefined
      ),
    enabled: canReports,
  });

  const deviceCount = dashboard?.rooms.reduce((n, r) => n + r.devices.length, 0) ?? 0;
  const lowStock = items.filter((i) => i.isLowStock && i.isActive).length;
  const openSessions = sessions.length;
  const branchLabel =
    activeBranchId == null && user?.isMaster
      ? t('common.allBranches')
      : user?.branches.find((b) => b.id === activeBranchId)?.name ?? dashboard?.branchName ?? '';

  const quickLinks = [
    { to: '/floor', icon: 'floor' as const, label: t('nav.floor'), desc: t('home.openFloor') },
    { to: '/cafeteria', icon: 'cafeteria' as const, label: t('nav.cafeteria'), desc: t('home.openCafeteria') },
    { to: '/inventory', icon: 'inventory' as const, label: t('nav.inventory'), desc: t('home.openInventory') },
    { to: '/settings', icon: 'settings' as const, label: t('nav.settings'), desc: t('home.openSettings') },
  ];

  return (
    <div className="space-y-6">
      <div className="mb-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <PageHeader title={t('home.title')} />
          <p className="-mt-4 mb-2 text-sm text-muted">
            {t('home.welcome', { name: user?.firstName ?? '' })}
          </p>
          {branchLabel && (
            <p className="mb-4 text-sm text-muted">
              {withBranchName(t('home.title'), branchLabel)}
            </p>
          )}
        </div>
        {user?.isMaster && (
          <BranchScopeSelect value={scopeValue} onChange={handleScopeChange} />
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label={withBranchName(t('home.openSessions'), branchLabel)}
          value={String(openSessions)}
          accent="primary"
        />
        <StatCard label={withBranchName(t('home.devices'), branchLabel)} value={String(deviceCount)} />
        <StatCard
          label={withBranchName(t('home.lowStock'), branchLabel)}
          value={String(lowStock)}
          accent={lowStock > 0 ? 'danger' : 'success'}
        />
        <StatCard
          label={withBranchName(t('home.todayRevenue'), branchLabel)}
          value={canReports && finance ? formatCurrency(finance.totalRevenue) : '—'}
          accent="success"
        />
      </div>

      {canReports && finance && finance.byBranch?.length > 1 && activeBranchId == null && (
        <Card>
          <p className="mb-3 text-sm font-medium">{t('home.byBranch')}</p>
          <ul className="space-y-2 text-sm">
            {finance.byBranch.map((b) => (
              <li key={b.branchId} className="flex flex-wrap justify-between gap-2 border-b border-border pb-2 last:border-0">
                <span>{b.branchName}</span>
                <span className="text-muted">
                  {formatCurrency(b.revenue)} · {t('home.net')}: {formatCurrency(b.netProfit)}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {items.filter((i) => i.isLowStock && i.isActive).length > 0 && (
        <Card>
          <p className="mb-3 text-sm font-medium">{withBranchName(t('home.lowStock'), branchLabel)}</p>
          <ul className="space-y-1 text-sm">
            {items
              .filter((i) => i.isLowStock && i.isActive)
              .slice(0, 8)
              .map((i) => {
                const name = i.nameAr && user ? i.name : i.name;
                const bName = user?.branches.find((b) => b.id === i.branchId)?.name;
                return (
                  <li key={i.id} className="text-muted">
                    {withBranchName(name, bName || branchLabel)}
                  </li>
                );
              })}
          </ul>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {quickLinks.map((link) => (
          <Link key={link.to} to={link.to}>
            <Card className="h-full transition-colors hover:border-primary/40 hover:bg-surface-hover">
              <div className="flex items-start gap-3">
                <span className="rounded-xl bg-primary/15 p-2.5 text-primary">
                  <Icon name={link.icon} className="h-5 w-5" />
                </span>
                <div>
                  <p className="font-semibold">{link.label}</p>
                  <p className="mt-1 text-xs text-muted">{link.desc}</p>
                </div>
              </div>
            </Card>
          </Link>
        ))}
      </div>

      <Card className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="font-semibold">{withBranchName(t('home.floorTitle'), branchLabel)}</p>
          <p className="text-sm text-muted">{t('home.floorHint')}</p>
        </div>
        <Link to="/floor">
          <Button>
            <Icon name="play" className="h-4 w-4" />
            {t('home.goToFloor')}
          </Button>
        </Link>
      </Card>
    </div>
  );
}
