import type { AuthUser, PermissionInfo } from '@/types';

export function hasPermission(user: AuthUser | null, permission: string): boolean {
  if (!user) return false;
  return user.isMaster || user.permissions.includes(permission);
}

export const Permissions = {
  SessionsView: 'Sessions.View',
  SessionsCreate: 'Sessions.Create',
  SessionsPause: 'Sessions.Pause',
  SessionsClose: 'Sessions.Close',
  SessionsHistory: 'Sessions.History',
  CafeteriaView: 'Cafeteria.View',
  CafeteriaSell: 'Cafeteria.Sell',
  CafeteriaReturn: 'Cafeteria.Return',
  InventoryView: 'Inventory.View',
  InventoryAdjust: 'Inventory.Adjust',
  InventoryManageItems: 'Inventory.ManageItems',
  ExpensesView: 'Expenses.View',
  ExpensesAdd: 'Expenses.Add',
  ReportsView: 'Reports.View',
  SettingsManage: 'Settings.Manage',
  AssetsManage: 'Assets.Manage',
  UsersManage: 'Users.Manage',
  CustomersView: 'Customers.View',
  CustomersManage: 'Customers.Manage',
  OffersManage: 'Offers.Manage',
  CanEditClosedRecords: 'CanEditClosedRecords',
} as const;

/** Local catalog used when /users/permissions fails or returns empty. Keep in sync with PermissionSeed. */
export const PERMISSION_CATALOG: PermissionInfo[] = [
  { id: '1', code: 'Sessions.View', module: 'Sessions', action: 'View', description: 'View floor & active sessions' },
  { id: '2', code: 'Sessions.Create', module: 'Sessions', action: 'Create', description: 'Open session' },
  { id: '3', code: 'Sessions.Pause', module: 'Sessions', action: 'Pause', description: 'Pause/resume' },
  { id: '4', code: 'Sessions.Close', module: 'Sessions', action: 'Close', description: 'Close & bill' },
  { id: '5', code: 'Sessions.History', module: 'Sessions', action: 'History', description: 'Session open/close history' },
  { id: '6', code: 'Cafeteria.View', module: 'Cafeteria', action: 'View', description: 'View cafeteria' },
  { id: '7', code: 'Cafeteria.Sell', module: 'Cafeteria', action: 'Sell', description: 'Dispense / sell' },
  { id: '8', code: 'Cafeteria.Return', module: 'Cafeteria', action: 'Return', description: 'Returns' },
  { id: '9', code: 'Inventory.View', module: 'Inventory', action: 'View', description: 'View stock' },
  { id: '10', code: 'Inventory.Adjust', module: 'Inventory', action: 'Adjust', description: 'Stock vouchers' },
  { id: '11', code: 'Inventory.ManageItems', module: 'Inventory', action: 'ManageItems', description: 'Items & units' },
  { id: '14', code: 'Expenses.View', module: 'Expenses', action: 'View', description: 'View expenses' },
  { id: '15', code: 'Expenses.Add', module: 'Expenses', action: 'Add', description: 'Add expense' },
  { id: '16', code: 'Reports.View', module: 'Reports', action: 'View', description: 'View reports' },
  { id: '17', code: 'Assets.Manage', module: 'Assets', action: 'Manage', description: 'Rooms & devices' },
  { id: '18', code: 'Settings.Manage', module: 'Settings', action: 'Manage', description: 'Settings' },
  { id: '19', code: 'Users.Manage', module: 'Users', action: 'Manage', description: 'Manage users' },
  { id: '20', code: 'Customers.View', module: 'Customers', action: 'View', description: 'View and search customers' },
  { id: '21', code: 'Customers.Manage', module: 'Customers', action: 'Manage', description: 'Create, update, and delete customers; send WhatsApp messages' },
  { id: '22', code: 'Offers.Manage', module: 'Offers', action: 'Manage', description: 'Create and manage customer offers' },
  { id: '23', code: 'CanEditClosedRecords', module: 'Security', action: 'EditClosed', description: 'Edit closed records' },
];

export function normalizePermissionCatalog(raw: unknown): PermissionInfo[] {
  if (!Array.isArray(raw) || raw.length === 0) return PERMISSION_CATALOG;

  const mapped = raw
    .map((item) => {
      const p = item as Record<string, unknown>;
      const code = String(p.code ?? p.Code ?? '');
      const module = String(p.module ?? p.Module ?? '');
      const action = String(p.action ?? p.Action ?? '');
      const description = String(p.description ?? p.Description ?? '');
      const id = String(p.id ?? p.Id ?? code);
      if (!code || !module) return null;
      return { id, code, module, action, description } satisfies PermissionInfo;
    })
    .filter((p): p is PermissionInfo => p !== null)
    // Purchase orders feature was removed from the UI — hide its permissions.
    .filter((p) => p.module !== 'PurchaseOrders');

  return mapped.length > 0 ? mapped : PERMISSION_CATALOG;
}
