import { useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { branchesApi, usersApi } from '@/api/client';
import { normalizePermissionCatalog } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { ManagedUser, PermissionInfo } from '@/types';
import { NotificationChannel, UserRole } from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

function addMonthsIso(months: number, from?: string) {
  const base = from ? new Date(from) : new Date();
  const d = new Date(Date.UTC(base.getUTCFullYear(), base.getUTCMonth(), base.getUTCDate()));
  if (d.getTime() < Date.now()) {
    const now = new Date();
    d.setUTCFullYear(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  }
  d.setUTCMonth(d.getUTCMonth() + months);
  return d.toISOString().slice(0, 10);
}

export function UsersPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const queryClient = useQueryClient();
  const canManage = !!user?.isMaster;
  const isSuperAdmin = user?.role === UserRole.SuperAdmin || (user?.isMaster && user?.role == null);

  const [open, setOpen] = useState(false);
  const [editUser, setEditUser] = useState<ManagedUser | null>(null);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [role, setRole] = useState<number>(UserRole.Staff);
  const [allowedChannels, setAllowedChannels] = useState<number>(NotificationChannel.EmailAndWhatsApp);
  const [isActive, setIsActive] = useState(true);
  const [subscriptionExpiresAt, setSubscriptionExpiresAt] = useState('');
  const [selectedPerms, setSelectedPerms] = useState<string[]>([]);
  const [selectedBranches, setSelectedBranches] = useState<string[]>([]);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const { data: usersPage, isLoading } = useQuery({
    queryKey: ['users', page, pageSize],
    queryFn: () => usersApi.getAll(page, pageSize),
    enabled: canManage,
  });
  const users = usersPage?.items ?? [];

  const { data: permissions = [] } = useQuery({
    queryKey: ['user-permissions-catalog'],
    queryFn: async () => normalizePermissionCatalog(await usersApi.getPermissions()),
    enabled: canManage,
    initialData: normalizePermissionCatalog([]),
  });

  const { data: branches = [] } = useQuery({
    queryKey: ['branches'],
    queryFn: branchesApi.getAll,
    enabled: canManage,
  });

  // Staff can never manage users, so the Users permission module is not offered.
  const grantablePermissions = useMemo(
    () => permissions.filter((p) => p.module !== 'Users'),
    [permissions],
  );

  const permsByModule = useMemo(() => {
    const moduleOrder = [
      'Sessions',
      'Cafeteria',
      'Inventory',
      'PurchaseOrders',
      'Expenses',
      'Reports',
      'Assets',
      'Customers',
      'Offers',
      'Settings',
      'Security',
    ];
    const map = new Map<string, PermissionInfo[]>();
    for (const p of grantablePermissions) {
      const list = map.get(p.module) ?? [];
      list.push(p);
      map.set(p.module, list);
    }
    const ordered = moduleOrder
      .filter((m) => map.has(m))
      .map((m) => [m, map.get(m)!] as const);
    for (const [m, list] of map) {
      if (!moduleOrder.includes(m)) ordered.push([m, list]);
    }
    return ordered;
  }, [grantablePermissions]);

  function permKey(code: string) {
    return code.replace(/\./g, '_');
  }

  function moduleTitle(module: string) {
    return t(`perm.modules.${module}`, { defaultValue: module });
  }

  function permTitle(code: string) {
    return t(`perm.codes.${permKey(code)}.label`, { defaultValue: code });
  }

  function permDescription(code: string, fallback: string) {
    return t(`perm.codes.${permKey(code)}.desc`, { defaultValue: fallback });
  }

  function setModulePerms(modulePerms: PermissionInfo[], select: boolean) {
    const codes = modulePerms.map((p) => p.code);
    setSelectedPerms((prev) => {
      if (select) return [...new Set([...prev, ...codes])];
      return prev.filter((c) => !codes.includes(c));
    });
  }

  function setAllPerms(select: boolean) {
    if (!select) {
      setSelectedPerms([]);
      return;
    }
    setSelectedPerms(grantablePermissions.map((p) => p.code));
  }

  const allPermCodes = grantablePermissions.map((p) => p.code);
  const allPermsSelected =
    allPermCodes.length > 0 && allPermCodes.every((c) => selectedPerms.includes(c));

  function resetForm() {
    setUsername('');
    setPassword('');
    setFirstName('');
    setLastName('');
    setRole(isSuperAdmin ? UserRole.MasterAdmin : UserRole.Staff);
    setAllowedChannels(NotificationChannel.EmailAndWhatsApp);
    setIsActive(true);
    setSubscriptionExpiresAt('');
    setSelectedPerms([]);
    setSelectedBranches([]);
    setError('');
    setEditUser(null);
  }

  function openCreate() {
    resetForm();
    setOpen(true);
  }

  function openEdit(u: ManagedUser) {
    setEditUser(u);
    setUsername(u.username);
    setPassword('');
    setFirstName(u.firstName);
    setLastName(u.lastName);
    setRole(u.role ?? (u.isMaster ? UserRole.SuperAdmin : UserRole.Staff));
    setAllowedChannels(u.allowedNotificationChannels ?? NotificationChannel.EmailAndWhatsApp);
    setIsActive(u.isActive);
    setSubscriptionExpiresAt(u.subscriptionExpiresAt ? u.subscriptionExpiresAt.slice(0, 10) : '');
    setSelectedPerms(u.permissions.filter((p) => p !== '*'));
    {
      const editRole = u.role ?? (u.isMaster ? UserRole.SuperAdmin : UserRole.Staff);
      const privileged = editRole === UserRole.SuperAdmin || editRole === UserRole.MasterAdmin;
      setSelectedBranches(
        privileged
          ? []
          : u.branchIds.length
            ? u.branchIds
            : branches[0]
              ? [branches[0].id]
              : [],
      );
    }
    setError('');
    setOpen(true);
  }

  function roleLabel(r: number) {
    if (r === UserRole.SuperAdmin) return t('users.superAdmin');
    if (r === UserRole.MasterAdmin) return t('users.masterAdmin');
    return t('users.staff');
  }

  const isPrivilegedRole = role === UserRole.SuperAdmin || role === UserRole.MasterAdmin;

  function togglePerm(code: string) {
    setSelectedPerms((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]
    );
  }

  function toggleBranch(id: string) {
    setSelectedBranches((prev) =>
      prev.includes(id) ? prev.filter((b) => b !== id) : [...prev, id]
    );
  }

  const createMutation = useMutation({
    mutationFn: () =>
      usersApi.create({
        username,
        password,
        firstName,
        lastName,
        role,
        isMaster: isPrivilegedRole,
        subscriptionExpiresAt: isSuperAdmin ? subscriptionExpiresAt || null : null,
        allowedNotificationChannels: isSuperAdmin && isPrivilegedRole ? allowedChannels : undefined,
        permissionCodes: role === UserRole.Staff ? selectedPerms : undefined,
        branchIds: isPrivilegedRole ? [] : selectedBranches,
      }),
    onSuccess: () => {
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      usersApi.update(editUser!.id, {
        firstName,
        lastName,
        isActive,
        role,
        isMaster: isPrivilegedRole,
        subscriptionExpiresAt: isSuperAdmin ? subscriptionExpiresAt || null : editUser!.subscriptionExpiresAt,
        allowedNotificationChannels: isSuperAdmin && isPrivilegedRole ? allowedChannels : undefined,
        permissionCodes: role === UserRole.Staff ? selectedPerms : undefined,
        branchIds: isPrivilegedRole ? [] : selectedBranches,
      }),
    onSuccess: async () => {
      if (password.trim()) {
        await usersApi.resetPassword(editUser!.id, password);
      }
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => usersApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
    onError: (e: Error) => setError(e.message),
  });

  if (!canManage) return <Navigate to="/" replace />;

  return (
    <div>
      <PageHeader title={t('users.title')}>
        <Button onClick={openCreate}>
          <Icon name="plus" className="h-4 w-4" />
          {t('users.addUser')}
        </Button>
      </PageHeader>

      <p className="mb-6 max-w-2xl text-sm text-muted">{t('users.hint')}</p>

      {isLoading ? (
        <PageLoader />
      ) : (
        <>
        <DataTable
          headers={[
            t('users.username'),
            t('users.name'),
            t('users.role'),
            t('users.branches'),
            t('users.status'),
            t('users.subscriptionEnds'),
            t('users.permissions'),
            '',
          ]}
        >
          {users.map((u) => (
            <tr key={u.id} className="hover:bg-surface-hover">
              <td className="px-4 py-3 font-medium">{u.username}</td>
              <td className="px-4 py-3">
                {u.firstName} {u.lastName}
              </td>
              <td className="px-4 py-3">
                {u.role === UserRole.Staff || (!u.isMaster && !u.role) ? (
                  t('users.staff')
                ) : (
                  <span className="inline-flex items-center gap-1 text-primary">
                    <Icon name="shield" className="h-3.5 w-3.5" />
                    {roleLabel(u.role ?? UserRole.SuperAdmin)}
                  </span>
                )}
              </td>
              <td className="px-4 py-3 text-xs text-muted">
                {(u.branchNames?.length ?? 0) > 0
                  ? u.branchNames.join(' · ')
                  : t('users.noBranchesAssigned')}
              </td>
              <td className="px-4 py-3">
                <Badge status={u.isActive ? 'watching' : 'paused'}>
                  {u.isActive ? t('users.active') : t('users.inactive')}
                </Badge>
              </td>
              <td className="px-4 py-3 text-sm text-muted">
                {u.subscriptionExpiresAt
                  ? new Date(u.subscriptionExpiresAt).toLocaleDateString()
                  : t('users.noExpiry')}
                {u.subscriptionLockedAt && (
                  <span className="mt-0.5 block text-xs text-danger">{t('users.lockedBySubscription')}</span>
                )}
              </td>
              <td className="px-4 py-3 text-xs text-muted">
                {u.isMaster ? t('users.allPermissions') : `${u.permissions.length} ${t('users.assigned')}`}
              </td>
              <td className="px-4 py-3">
                <div className="flex flex-wrap gap-1">
                  <Button variant="ghost" size="sm" onClick={() => openEdit(u)}>
                    {t('users.edit')}
                  </Button>
                  {u.id !== user?.id && (
                    <Button
                      variant="danger"
                      size="sm"
                      loading={deleteMutation.isPending}
                      onClick={() => {
                        if (window.confirm(t('common.confirmDelete'))) {
                          deleteMutation.mutate(u.id);
                        }
                      }}
                    >
                      {t('common.delete')}
                    </Button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </DataTable>
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={usersPage?.totalCount ?? 0}
          onPageChange={setPage}
          onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
        />
        </>
      )}

      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title={editUser ? t('users.editUser') : t('users.addUser')}
        size="xl"
      >
        <div className="space-y-3 pe-1">
          {!editUser && (
            <Input label={t('users.username')} value={username} onChange={(e) => setUsername(e.target.value)} />
          )}
          {editUser && (
            <p className="text-sm text-muted">
              {t('users.username')}: <span className="text-text">{username}</span>
            </p>
          )}
          <div className="grid grid-cols-2 gap-3">
            <Input label={t('auth.firstName')} value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            <Input label={t('auth.lastName')} value={lastName} onChange={(e) => setLastName(e.target.value)} />
          </div>
          <Input
            label={editUser ? t('users.newPasswordOptional') : t('auth.password')}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />

          <div>
            <p className="mb-2 text-sm font-medium">{t('users.role')}</p>
            <p className="text-sm">{roleLabel(role)}</p>
            <p className="mt-1 text-xs text-muted">
              {role === UserRole.MasterAdmin
                ? t('users.masterAdminNoBranchesHint')
                : role === UserRole.SuperAdmin
                  ? t('users.masterHint')
                  : t('users.permissionsHint')}
            </p>
          </div>

          {isSuperAdmin && isPrivilegedRole && (
            <div>
              <p className="mb-2 text-sm font-medium">{t('users.notificationChannels')}</p>
              <p className="mb-2 text-xs text-muted">{t('users.notificationChannelsHint')}</p>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  size="sm"
                  variant={allowedChannels === NotificationChannel.Email ? 'primary' : 'secondary'}
                  onClick={() => setAllowedChannels(NotificationChannel.Email)}
                >
                  {t('users.channelEmailOnly')}
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant={allowedChannels === NotificationChannel.EmailAndWhatsApp ? 'primary' : 'secondary'}
                  onClick={() => setAllowedChannels(NotificationChannel.EmailAndWhatsApp)}
                >
                  {t('users.channelEmailWhatsApp')}
                </Button>
              </div>
            </div>
          )}

          {editUser && (
            <>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                {t('users.active')}
              </label>
              <p className="text-xs text-muted">{t('users.activeOverrideHint')}</p>
            </>
          )}

          {isSuperAdmin && (
          <div>
            <Input
              label={t('users.subscriptionEnds')}
              type="date"
              value={subscriptionExpiresAt}
              onChange={(e) => setSubscriptionExpiresAt(e.target.value)}
            />
            <p className="mt-1 text-xs text-muted">{t('users.subscriptionHint')}</p>
            <div className="mt-2 flex flex-wrap gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSubscriptionExpiresAt(addMonthsIso(1, subscriptionExpiresAt));
                  if (editUser) setIsActive(true);
                }}
              >
                {t('users.renew1Month')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSubscriptionExpiresAt(addMonthsIso(3, subscriptionExpiresAt));
                  if (editUser) setIsActive(true);
                }}
              >
                {t('users.renew3Months')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSubscriptionExpiresAt(addMonthsIso(12, subscriptionExpiresAt));
                  if (editUser) setIsActive(true);
                }}
              >
                {t('users.renew1Year')}
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setSubscriptionExpiresAt('')}>
                {t('users.clearExpiry')}
              </Button>
            </div>
          </div>
          )}

          {!isPrivilegedRole && (
          <div>
            <p className="mb-2 text-sm font-medium">{t('users.branches')}</p>
            <p className="mb-2 text-xs text-muted">{t('users.staffBranchesHint')}</p>
            {branches.length === 0 ? (
              <p className="rounded-lg border border-warning/30 bg-warning/10 px-3 py-2 text-sm text-warning">
                {t('users.noBranchesYet')}
              </p>
            ) : (
              <div className="grid grid-cols-1 gap-1 rounded-lg border border-border p-3 sm:grid-cols-2">
                {branches.map((b) => (
                  <label key={b.id} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={selectedBranches.includes(b.id)}
                      onChange={() => toggleBranch(b.id)}
                    />
                    {b.name}
                  </label>
                ))}
              </div>
            )}
          </div>
          )}

          {isPrivilegedRole ? (
            <p className="rounded-lg border border-primary/30 bg-primary/10 px-3 py-2 text-sm text-primary">
              {role === UserRole.MasterAdmin ? t('users.masterAdminNoBranchesHint') : t('users.masterHint')}
            </p>
          ) : (
            <div>
              <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-medium">{t('users.permissions')}</p>
                <div className="flex flex-wrap items-center gap-2">
                  {selectedPerms.length > 0 && (
                    <span className="text-xs text-muted">
                      {t('users.selectedCount', { count: selectedPerms.length })}
                    </span>
                  )}
                  <Button
                    type="button"
                    size="sm"
                    variant="secondary"
                    onClick={() => setAllPerms(!allPermsSelected)}
                  >
                    {allPermsSelected ? t('users.clearAllPermissions') : t('users.selectAllPermissions')}
                  </Button>
                </div>
              </div>
              <p className="mb-2 text-xs text-muted">{t('users.permissionsHint')}</p>
              <div className="max-h-[28rem] overflow-y-auto rounded-lg border border-border p-3">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {permsByModule.map(([module, perms]) => {
                    const allSelected = perms.every((p) => selectedPerms.includes(p.code));
                    return (
                      <div key={module} className="rounded-lg bg-surface/60 p-2.5">
                        <div className="mb-2 flex items-center justify-between gap-2">
                          <p className="text-sm font-semibold text-text">{moduleTitle(module)}</p>
                          <button
                            type="button"
                            className="shrink-0 text-xs font-medium text-primary hover:underline"
                            onClick={() => setModulePerms(perms, !allSelected)}
                          >
                            {allSelected ? t('users.clearPage') : t('users.selectAllPage')}
                          </button>
                        </div>
                        <div className="space-y-2">
                          {perms.map((p) => (
                            <label key={p.code} className="flex items-start gap-2 text-sm">
                              <input
                                type="checkbox"
                                className="mt-0.5"
                                checked={selectedPerms.includes(p.code)}
                                onChange={() => togglePerm(p.code)}
                              />
                              <span>
                                <span className="font-medium">{permTitle(p.code)}</span>
                                <span className="block text-xs text-muted">
                                  {permDescription(p.code, p.description)}
                                </span>
                              </span>
                            </label>
                          ))}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
          )}

          {error && <p className="text-sm text-danger">{error}</p>}

          <Button
            className="w-full"
            loading={createMutation.isPending || updateMutation.isPending}
            disabled={
              !firstName.trim() ||
              !lastName.trim() ||
              (!isPrivilegedRole && selectedBranches.length === 0) ||
              (!editUser && (!username.trim() || !password.trim()))
            }
            onClick={() => (editUser ? updateMutation.mutate() : createMutation.mutate())}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
