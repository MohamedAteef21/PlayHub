import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { accountingApi, assetsApi, cafeteriaApi, platformApi, sessionsApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, isSuperAdmin, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { PageHeader, StatCard } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';

function SuperAdminHome() {
  const { t } = useTranslation();
  const { data, isLoading } = useQuery({
    queryKey: ['platform-dashboard'],
    queryFn: platformApi.getDashboard,
  });

  if (isLoading || !data) return <PageLoader />;

  return (
    <div className="space-y-6">
      <div className="mb-2">
        <PageHeader title={t('superAdmin.dashboardTitle')} />
        <p className="-mt-4 mb-6 text-sm text-muted">{t('superAdmin.dashboardHint')}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label={t('superAdmin.masters')} value={String(data.mastersCount)} accent="primary" />
        <StatCard label={t('superAdmin.activeMasters')} value={String(data.activeMastersCount)} accent="success" />
        <StatCard
          label={t('superAdmin.expiring7')}
          value={String(data.expiringWithin7Days)}
          accent={data.expiringWithin7Days > 0 ? 'danger' : 'success'}
        />
        <StatCard
          label={t('superAdmin.lockedOrExpired')}
          value={String(data.expiredOrLocked)}
          accent={data.expiredOrLocked > 0 ? 'danger' : undefined}
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label={t('superAdmin.staff')} value={String(data.staffCount)} />
        <StatCard label={t('superAdmin.totalUsers')} value={String(data.totalUsers)} />
        <StatCard label={t('superAdmin.inactiveMasters')} value={String(data.inactiveMastersCount)} />
        <StatCard label={t('superAdmin.expiring30')} value={String(data.expiringWithin30Days)} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <div className="mb-3 flex items-center justify-between gap-2">
            <p className="font-semibold">{t('superAdmin.upcomingTitle')}</p>
            <Link to="/users" className="text-xs font-medium text-primary hover:underline">
              {t('superAdmin.manageUsers')}
            </Link>
          </div>
          {data.upcomingExpiries.length === 0 ? (
            <p className="text-sm text-muted">{t('superAdmin.noUpcoming')}</p>
          ) : (
            <ul className="divide-y divide-border">
              {data.upcomingExpiries.map((row) => (
                <li key={row.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.fullName || row.username}</p>
                    <p className="truncate text-xs text-muted" dir="ltr">
                      {row.username}
                    </p>
                  </div>
                  <div className="shrink-0 text-end">
                    <p className="text-xs text-muted">
                      {row.subscriptionExpiresAt
                        ? new Date(row.subscriptionExpiresAt).toLocaleDateString()
                        : '—'}
                    </p>
                    <p className={`text-xs font-medium ${row.daysLeft != null && row.daysLeft <= 7 ? 'text-danger' : 'text-warning'}`}>
                      {row.daysLeft == null
                        ? t('users.noExpiry')
                        : t('superAdmin.daysLeft', { count: row.daysLeft })}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <p className="mb-3 font-semibold">{t('superAdmin.lockedTitle')}</p>
          {data.lockedOrExpired.length === 0 ? (
            <p className="text-sm text-muted">{t('superAdmin.noLocked')}</p>
          ) : (
            <ul className="divide-y divide-border">
              {data.lockedOrExpired.map((row) => (
                <li key={row.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.fullName || row.username}</p>
                    <p className="truncate text-xs text-muted" dir="ltr">
                      {row.username}
                    </p>
                  </div>
                  <div className="shrink-0 text-end text-xs text-danger">
                    {row.isLocked ? t('users.lockedBySubscription') : t('superAdmin.expired')}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}

export function HomeDashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);

  if (isSuperAdmin(user)) {
    return <SuperAdminHome />;
  }

  const canReports = hasPermission(user, Permissions.ReportsView);

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
    queryFn: () => accountingApi.getDashboard(toIsoDate(day), toIsoDateEnd(day)),
    enabled: canReports,
  });

  const deviceCount = dashboard?.rooms.reduce((n, r) => n + r.devices.length, 0) ?? 0;
  const lowStock = items.filter((i) => i.isLowStock && i.isActive).length;
  const openSessions = sessions.length;

  const quickLinks = [
    { to: '/floor', icon: 'floor' as const, label: t('nav.floor'), desc: t('home.openFloor') },
    { to: '/cafeteria', icon: 'cafeteria' as const, label: t('nav.cafeteria'), desc: t('home.openCafeteria') },
    { to: '/inventory', icon: 'inventory' as const, label: t('nav.inventory'), desc: t('home.openInventory') },
    { to: '/settings', icon: 'settings' as const, label: t('nav.settings'), desc: t('home.openSettings') },
  ];

  return (
    <div className="space-y-6">
      <div className="mb-2">
        <PageHeader title={t('home.title')} />
        <p className="-mt-4 mb-6 text-sm text-muted">
          {t('home.welcome', { name: user?.firstName ?? '' })}
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label={t('home.openSessions')} value={String(openSessions)} accent="primary" />
        <StatCard label={t('home.devices')} value={String(deviceCount)} />
        <StatCard
          label={t('home.lowStock')}
          value={String(lowStock)}
          accent={lowStock > 0 ? 'danger' : 'success'}
        />
        <StatCard
          label={t('home.todayRevenue')}
          value={canReports && finance ? formatCurrency(finance.totalRevenue) : '—'}
          accent="success"
        />
      </div>

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
          <p className="font-semibold">{t('home.floorTitle')}</p>
          <p className="text-sm text-muted">{t('home.floorHint')}</p>
        </div>
        <Link to="/floor">
          <Button>{t('home.openFloor')}</Button>
        </Link>
      </Card>
    </div>
  );
}
