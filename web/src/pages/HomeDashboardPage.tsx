import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { accountingApi, assetsApi, cafeteriaApi, sessionsApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { PageHeader, StatCard } from '@/components/ui/PageHelpers';

export function HomeDashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
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

      {(dashboard?.equipment?.length ?? 0) > 0 && (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {dashboard!.equipment.filter((e) => e.isActive).map((e) => (
            <Card key={e.id} className="!p-3">
              <p className="text-sm font-semibold">{e.name}</p>
              <p className="mt-1 text-xs text-muted">
                {t('dashboard.equipmentFree')}: {e.freeQuantity}
                {' · '}
                {t('dashboard.equipmentInUse')}: {e.inUseQuantity}
              </p>
              <p className="text-xs text-muted">
                {t('dashboard.equipmentMaintenance')}: {e.maintenanceQuantity}
                {' / '}
                {t('dashboard.equipmentTotal')}: {e.totalQuantity}
              </p>
            </Card>
          ))}
        </div>
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
          <p className="font-semibold">{t('home.floorTitle')}</p>
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
