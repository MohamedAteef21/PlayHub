import { Outlet, NavLink, useNavigate, Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useEffect, useRef, useState } from 'react';
import { queryClient } from '@/App';
import { useAuthStore, useUiStore } from '@/store';
import { authApi } from '@/api/client';
import { Icon, type IconName } from '@/components/ui/Icons';
import { GlobalBusyOverlay } from '@/components/ui/GlobalBusyOverlay';
import { hasPermission, isSuperAdmin, Permissions } from '@/lib/permissions';

const navItems: {
  to: string;
  key: string;
  icon: IconName;
  masterOnly?: boolean;
  permission?: string;
  anyPermission?: string[];
}[] = [
  { to: '/', key: 'dashboard', icon: 'dashboard' },
  { to: '/floor', key: 'floor', icon: 'floor', permission: Permissions.SessionsView },
  { to: '/sessions', key: 'sessions', icon: 'clock', permission: Permissions.SessionsHistory },
  { to: '/cafeteria', key: 'cafeteria', icon: 'cafeteria', permission: Permissions.CafeteriaView },
  { to: '/inventory', key: 'inventory', icon: 'inventory', permission: Permissions.InventoryView },
  { to: '/accounting', key: 'accounting', icon: 'accounting', permission: Permissions.ExpensesView },
  { to: '/reports', key: 'reports', icon: 'reports', permission: Permissions.ReportsView },
  {
    to: '/customers',
    key: 'customers',
    icon: 'customers',
    anyPermission: [Permissions.CustomersView, Permissions.CustomersManage],
  },
  { to: '/users', key: 'users', icon: 'users', masterOnly: true },
  { to: '/activity', key: 'activity', icon: 'clock', masterOnly: true },
  { to: '/settings', key: 'settings', icon: 'settings', permission: Permissions.SettingsManage },
];

export function AppLayout() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, activeBranchId, logout, refreshToken, setAuth, patchUser } = useAuthStore();
  const {
    language,
    setLanguage,
    theme,
    setTheme,
    sidebarOpen,
    toggleSidebar,
    sidebarCollapsed,
    toggleSidebarCollapsed,
  } = useUiStore();
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [switchingBranch, setSwitchingBranch] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

  async function persistUiPreferences(next: { language?: 'en' | 'ar'; theme?: 'dark' | 'light' }) {
    const preferredLanguage = next.language ?? language;
    const preferredTheme = next.theme ?? theme;
    try {
      const updated = await authApi.updatePreferences({ preferredLanguage, preferredTheme });
      patchUser({
        preferredLanguage: updated.preferredLanguage,
        preferredTheme: updated.preferredTheme,
      });
    } catch {
      // keep local UI even if sync fails
    }
  }

  function handleLanguageToggle() {
    const next = language === 'en' ? 'ar' : 'en';
    setLanguage(next);
    void persistUiPreferences({ language: next });
  }

  function handleThemeToggle() {
    const next = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);
    void persistUiPreferences({ theme: next });
  }

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (!userMenuRef.current?.contains(e.target as Node)) setUserMenuOpen(false);
    }
    if (userMenuOpen) document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [userMenuOpen]);

  // Super Admin: no venue branch required — stay on dashboard/users.
  useEffect(() => {
    if (!user) return;
    if (isSuperAdmin(user)) return;
    if (activeBranchId) return;
    // Master Admin with no venues yet: stay in the app and create a branch from Settings.
    if (user.isMaster && user.branches.length === 0) return;
    if (user.isMaster && user.branches.length > 0) {
      void (async () => {
        try {
          const res = await authApi.selectBranch(user.branches[0].id);
          setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
        } catch {
          navigate('/select-branch');
        }
      })();
      return;
    }
    navigate('/select-branch');
  }, [user, activeBranchId, setAuth, navigate]);

  if (!user) {
    navigate('/login');
    return null;
  }

  const superAdmin = isSuperAdmin(user);
  const needsFirstBranch = !superAdmin && user.isMaster && user.branches.length === 0 && !activeBranchId;

  if (!activeBranchId && !needsFirstBranch && !superAdmin) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-surface text-muted">
        {t('common.loading')}
      </div>
    );
  }

  const branchName = activeBranchId
    ? user.branches.find((b) => b.id === activeBranchId)?.name ?? ''
    : '';
  const displayName = `${user.firstName} ${user.lastName}`.trim() || user.email;
  const roleLabel =
    user.role === 2
      ? t('users.superAdmin')
      : user.role === 1
        ? t('users.masterAdmin')
        : user.isMaster
          ? t('users.superAdmin')
          : t('users.staff');
  const canSwitchBranch = user.branches.length > 1;

  const subscriptionWarning = (() => {
    if (!user.subscriptionExpiresAt) return null;
    const expiry = new Date(user.subscriptionExpiresAt);
    if (Number.isNaN(expiry.getTime())) return null;
    const today = new Date();
    const startOfToday = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
    const startOfExpiry = Date.UTC(expiry.getUTCFullYear(), expiry.getUTCMonth(), expiry.getUTCDate());
    const daysLeft = Math.ceil((startOfExpiry - startOfToday) / (24 * 60 * 60 * 1000));
    if (daysLeft < 0 || daysLeft > 7) return null;
    return { daysLeft, date: expiry.toLocaleDateString() };
  })();

  async function handleLogout() {
    setUserMenuOpen(false);
    if (refreshToken) await authApi.logout(refreshToken).catch(() => {});
    logout();
    queryClient.clear();
    navigate('/login');
  }

  async function handleSwitchBranch(branchId: string) {
    if (branchId === activeBranchId || switchingBranch) return;
    setSwitchingBranch(true);
    try {
      const res = await authApi.selectBranch(branchId);
      setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      setUserMenuOpen(false);
      window.location.assign('/');
    } catch {
      setSwitchingBranch(false);
    }
  }

  const railCollapsed = sidebarCollapsed;
  const visibleNav = navItems.filter((item) => {
    if (superAdmin) {
      return item.to === '/' || item.to === '/users' || item.to === '/settings';
    }
    if (item.masterOnly && !user.isMaster) return false;
    if (item.anyPermission?.length) {
      return item.anyPermission.some((p) => hasPermission(user, p));
    }
    if (item.permission && !hasPermission(user, item.permission)) return false;
    return true;
  });

  return (
    <div className="layout-stage flex min-h-screen text-text">
      <GlobalBusyOverlay />

      {/* Mobile drawer overlay */}
      {sidebarOpen && (
        <button
          type="button"
          className="fixed inset-0 z-30 bg-black/45 backdrop-blur-[2px] lg:hidden"
          aria-label={t('common.closeMenu')}
          onClick={toggleSidebar}
        />
      )}

      {/* One metallic chrome piece: rail + topbar */}
      <div className="chrome-shell relative z-20 flex min-h-screen w-full">
        <aside
          className={[
            'chrome-rail fixed inset-y-0 start-0 z-40 flex flex-col transition-[width,transform] duration-300 ease-out lg:static lg:translate-x-0',
            railCollapsed ? 'lg:w-[4.75rem]' : 'lg:w-64',
            'w-64',
            sidebarOpen ? 'translate-x-0' : '-translate-x-full',
          ].join(' ')}
        >
          <div
            className={[
              'flex h-14 shrink-0 items-center gap-3 px-3',
              railCollapsed ? 'lg:justify-center lg:px-2' : '',
            ].join(' ')}
          >
            <Link to="/" className="flex min-w-0 items-center gap-3" title="PlayHub">
              <span className="chrome-mark flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-white">
                <Icon name="gaming" className="h-5 w-5" />
              </span>
              <div className={railCollapsed ? 'lg:hidden' : ''}>
                <h1 className="font-display text-lg font-bold uppercase tracking-wide">PlayHub</h1>
                <p className="truncate text-[11px] text-muted">{t('app.tagline')}</p>
              </div>
            </Link>
          </div>

          <nav className="flex-1 space-y-1 overflow-y-auto px-2 pb-4 pt-1">
            {visibleNav.map(({ to, key, icon }) => (
              <NavLink
                key={to}
                to={to}
                end={to === '/'}
                title={t(`nav.${key}`)}
                onClick={() => {
                  if (window.matchMedia('(max-width: 1023px)').matches && sidebarOpen) {
                    toggleSidebar();
                  }
                }}
                className={({ isActive }) =>
                  [
                    'group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all',
                    railCollapsed ? 'lg:justify-center lg:px-0' : '',
                    isActive
                      ? 'chrome-nav-active text-primary'
                      : 'text-muted hover:bg-surface-hover/70 hover:text-text',
                  ].join(' ')
                }
              >
                <Icon name={icon} className="h-[18px] w-[18px] shrink-0 opacity-90" />
                <span className={railCollapsed ? 'lg:hidden' : ''}>{t(`nav.${key}`)}</span>
              </NavLink>
            ))}
          </nav>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="chrome-topbar sticky top-0 z-30 flex h-14 shrink-0 items-center gap-2 px-3 sm:px-4 lg:px-5">
            {/* Mobile open */}
            <button
              type="button"
              onClick={toggleSidebar}
              className="chrome-icon-btn rounded-lg p-2 text-muted hover:text-text lg:hidden"
              aria-label={t('common.openMenu')}
            >
              <Icon name="menu" className="h-5 w-5" />
            </button>

            {/* Desktop collapse / expand */}
            <button
              type="button"
              onClick={toggleSidebarCollapsed}
              className="chrome-icon-btn hidden rounded-lg p-2 text-muted hover:text-text lg:inline-flex"
              aria-label={railCollapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
              title={railCollapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
            >
              <Icon
                name={railCollapsed ? 'panelOpen' : 'panelClose'}
                className="rtl-flip h-5 w-5"
              />
            </button>

            <div className="ms-auto flex items-center gap-1 sm:gap-2">
              <button
                type="button"
                onClick={handleLanguageToggle}
                className="chrome-icon-btn rounded-lg px-2.5 py-1.5 text-sm font-semibold text-muted hover:text-text"
                title={language === 'en' ? 'العربية' : 'English'}
                aria-label="Language"
              >
                {language === 'en' ? 'عربي' : 'En'}
              </button>

              <button
                type="button"
                onClick={handleThemeToggle}
                className="chrome-icon-btn rounded-lg p-2 text-muted hover:text-text"
                title={theme === 'dark' ? t('common.lightMode') : t('common.darkMode')}
                aria-label="Theme"
              >
                <Icon name={theme === 'dark' ? 'sun' : 'moon'} className="h-5 w-5" />
              </button>

              <div className="relative" ref={userMenuRef}>
                <button
                  type="button"
                  onClick={() => setUserMenuOpen((o) => !o)}
                  className="chrome-user-chip flex max-w-[18rem] items-center gap-2 rounded-xl px-2.5 py-1.5 text-start sm:px-3"
                  aria-expanded={userMenuOpen}
                  aria-haspopup="menu"
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/15 text-primary">
                    <Icon name="user" className="h-4 w-4" />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium leading-tight">{displayName}</p>
                    <p className="flex items-center gap-1 truncate text-[11px] text-muted">
                      <Icon name="branch" className="h-3 w-3 shrink-0 text-primary" />
                      <span className="truncate">{branchName || t('branch.current')}</span>
                      <span className="text-border">·</span>
                      <span className="shrink-0">{roleLabel}</span>
                    </p>
                  </div>
                </button>

                {userMenuOpen && (
                  <div
                    role="menu"
                    className="absolute end-0 z-50 mt-2 w-60 overflow-hidden rounded-xl border border-border bg-surface-elevated py-1 shadow-lg animate-slide-in"
                  >
                    <div className="border-b border-border px-3 py-2">
                      <p className="truncate text-sm font-medium">{displayName}</p>
                      <p className="truncate text-xs text-muted">{roleLabel}</p>
                    </div>

                    {canSwitchBranch && (
                      <div className="border-b border-border py-1">
                        <p className="px-3 py-1.5 text-[11px] font-medium uppercase tracking-wide text-muted">
                          {t('branch.switch')}
                        </p>
                        {user.branches.map((b) => (
                          <button
                            key={b.id}
                            type="button"
                            role="menuitem"
                            disabled={switchingBranch}
                            className={`flex w-full items-center gap-2 px-3 py-2 text-sm hover:bg-surface-hover ${
                              b.id === activeBranchId ? 'bg-primary/10 text-primary' : ''
                            }`}
                            onClick={() => handleSwitchBranch(b.id)}
                          >
                            <Icon name="branch" className="h-4 w-4 shrink-0" />
                            <span className="truncate">{b.name}</span>
                            {b.id === activeBranchId && (
                              <span className="ms-auto text-[11px]">{t('branch.active')}</span>
                            )}
                          </button>
                        ))}
                      </div>
                    )}

                    {!canSwitchBranch && (
                      <div className="border-b border-border px-3 py-2">
                        <p className="text-[11px] text-muted">{t('branch.current')}</p>
                        <p className="truncate text-sm font-medium">{branchName}</p>
                      </div>
                    )}

                    {user.isMaster && (
                      <Link
                        to="/settings"
                        role="menuitem"
                        className="flex w-full items-center gap-2 px-3 py-2.5 text-sm hover:bg-surface-hover"
                        onClick={() => setUserMenuOpen(false)}
                      >
                        <Icon name="branch" className="h-4 w-4" />
                        {t('branch.manageBranches')}
                      </Link>
                    )}

                    <button
                      type="button"
                      role="menuitem"
                      className="flex w-full items-center gap-2 px-3 py-2.5 text-sm text-danger hover:bg-surface-hover"
                      onClick={handleLogout}
                    >
                      <Icon name="logout" className="h-4 w-4" />
                      {t('auth.logout')}
                    </button>
                  </div>
                )}
              </div>
            </div>
          </header>

          <main className="page-layer flex-1 p-3 sm:p-4 lg:p-5">
            <div className="page-panel min-h-[calc(100vh-5.5rem)] p-4 lg:p-6">
              {needsFirstBranch && (
                <div className="mb-4 rounded-xl border border-primary/35 bg-primary/10 px-4 py-3 text-sm text-primary">
                  <p className="font-medium">{t('branch.noBranchesYetTitle')}</p>
                  <p className="mt-1 text-muted">{t('branch.noBranchesYetHint')}</p>
                  <Link
                    to="/settings"
                    className="mt-2 inline-flex text-sm font-semibold text-primary underline-offset-2 hover:underline"
                  >
                    {t('branch.goCreateBranch')}
                  </Link>
                </div>
              )}
              {subscriptionWarning && (
                <div className="mb-4 rounded-xl border border-warning/40 bg-warning/10 px-4 py-3 text-sm text-warning">
                  {subscriptionWarning.daysLeft === 0
                    ? t('auth.subscriptionEndsToday')
                    : t('auth.subscriptionEndingSoon', {
                        days: subscriptionWarning.daysLeft,
                        date: subscriptionWarning.date,
                      })}
                </div>
              )}
              <div key={location.pathname} className="page-enter">
                <Outlet />
              </div>
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
