import { useAuthStore } from '@/store';
import type { AuthResponse } from '@/types';

/** Base server URL, e.g. "https://playhub.runasp.net" in production. Empty in dev (Vite proxy). */
export const SERVER_BASE = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ?? '';
const API_BASE = `${SERVER_BASE}/api`;

class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
    public data?: unknown
  ) {
    super(message);
  }
}

export { ApiError };

let refreshInFlight: Promise<string | null> | null = null;

function isSubscriptionExpiredPayload(body: { message?: string; code?: string } | null) {
  if (!body) return false;
  return (
    body.code === 'SUBSCRIPTION_EXPIRED' ||
    String(body.message ?? '').includes('SUBSCRIPTION_EXPIRED')
  );
}

/** Refresh access token. Never logs out on network blips — only on subscription expiry / invalid session. */
export async function refreshAccessToken(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    const { refreshToken, setAuth, logout } = useAuthStore.getState();
    if (!refreshToken) return null;

    try {
      const res = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({} as { message?: string; code?: string }));
        // Server answered — session is invalid or subscription ended. Network errors never reach here.
        if (res.status === 401 || isSubscriptionExpiredPayload(body)) {
          logout();
        }
        return null;
      }

      const data: AuthResponse = await res.json();
      setAuth(
        data.accessToken,
        data.refreshToken,
        data.user,
        data.activeBranchId,
        data.accessTokenExpiresAt
      );
      return data.accessToken;
    } catch {
      // Network offline / timeout — keep the user logged in; retry later
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const { accessToken, activeBranchId } = useAuthStore.getState();
  const isFormData = typeof FormData !== 'undefined' && options.body instanceof FormData;

  const headers: Record<string, string> = {
    ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
    ...(options.headers as Record<string, string>),
  };

  if (accessToken) headers['Authorization'] = `Bearer ${accessToken}`;
  if (activeBranchId) headers['X-Branch-Id'] = activeBranchId;

  let res: Response;
  try {
    res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  } catch {
    throw new ApiError('Network unavailable. Check your connection and try again.', 0, 'NETWORK');
  }

  if (res.status === 401 && accessToken) {
    const newToken = await refreshAccessToken();
    if (newToken) {
      headers['Authorization'] = `Bearer ${newToken}`;
      try {
        res = await fetch(`${API_BASE}${path}`, { ...options, headers });
      } catch {
        throw new ApiError('Network unavailable. Check your connection and try again.', 0, 'NETWORK');
      }
    }
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({ message: res.statusText }));
    const message = body.message ?? 'Request failed';
    if (isSubscriptionExpiredPayload(body)) {
      useAuthStore.getState().logout();
    }
    throw new ApiError(message, res.status, body.code, body);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export const authApi = {
  login: (username: string, password: string) =>
    apiFetch<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email: username, password }),
    }),

  selectBranch: (branchId: string) =>
    apiFetch<AuthResponse>('/auth/select-branch', {
      method: 'POST',
      body: JSON.stringify({ branchId }),
    }),

  logout: (refreshToken: string) =>
    apiFetch<void>('/auth/logout', {
      method: 'POST',
      body: JSON.stringify({ refreshToken }),
    }),

  updatePreferences: (data: { preferredLanguage?: string; preferredTheme?: string }) =>
    apiFetch<import('@/types').AuthUser>('/auth/preferences', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
};

export const sessionsApi = {
  getActive: () => apiFetch<import('@/types').SessionLive[]>('/sessions/active'),
  getHistory: (from?: string, to?: string, page = 1, pageSize = 20) => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return apiFetch<import('@/types').PagedResult<import('@/types').SessionHistory>>(
      `/sessions/history?${params}`
    );
  },
  open: (data: {
    deviceId: string;
    pricingPlanId: string;
    sessionMode: number;
    controllerCount?: number;
    watcherCount?: number;
    plannedDurationMinutes?: number;
    customerId?: string;
    isQuickGuest?: boolean;
    quickGuestName?: string;
  }) =>
    apiFetch<import('@/types').SessionLive>('/sessions/open', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  pause: (id: string) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${id}/pause`, { method: 'POST' }),
  resume: (id: string) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${id}/resume`, { method: 'POST' }),
  /** additionalMinutes = null → switch the session to an open (unlimited) timer */
  extend: (id: string, additionalMinutes: number | null) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${id}/extend`, {
      method: 'POST',
      body: JSON.stringify({ additionalMinutes }),
    }),
  convert: (id: string, data: { pricingPlanId: string; controllerCount: number }) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${id}/convert`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  close: (id: string, data: {
    payment: {
      paymentMethod: number;
      debtorName?: string;
      debtorPhone?: string;
      proofFileUrl?: string;
      walletAmount?: number;
    };
    discountAmount?: number;
    discountReason?: string;
  }) =>
    apiFetch<import('@/types').SessionDetail>(`/sessions/${id}/close`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  getById: (id: string) => apiFetch<import('@/types').SessionDetail>(`/sessions/${id}`),
  updateWatchers: (id: string, watcherCount: number) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${id}/watchers`, {
      method: 'POST',
      body: JSON.stringify({ watcherCount }),
    }),
  addCafeteria: (
    sessionId: string,
    cafeteriaItemId: string,
    variantId: string,
    quantity: number,
    stockDeductQuantity: number,
    customerName?: string,
    addOns?: { addOnId: string; quantity: number }[],
    allowSkipMissingIngredients?: boolean,
    unit?: number
  ) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${sessionId}/cafeteria`, {
      method: 'POST',
      body: JSON.stringify({
        cafeteriaItemId,
        variantId,
        quantity,
        stockDeductQuantity,
        customerName: customerName || undefined,
        addOns: addOns?.length ? addOns : undefined,
        allowSkipMissingIngredients: allowSkipMissingIngredients || undefined,
        unit: unit ?? undefined,
      }),
    }),
  returnCafeteria: (sessionId: string, sessionCafeteriaLineId: string, quantity: number, reason: string) =>
    apiFetch<import('@/types').SessionLive>(`/sessions/${sessionId}/cafeteria/returns`, {
      method: 'POST',
      body: JSON.stringify({ sessionCafeteriaLineId, quantity, reason }),
    }),
};

export const assetsApi = {
  getDashboard: () => apiFetch<import('@/types').AssetDashboard>('/assets/dashboard'),
  getRooms: () => apiFetch<import('@/types').Room[]>('/assets/rooms'),
  createRoom: (data: {
    name: string;
    roomNumber?: string;
    maxWatchingCapacity: number;
    assets?: { venueAssetTypeId: string; quantity: number; workingCount: number; notes?: string }[];
  }) =>
    apiFetch<import('@/types').Room>('/assets/rooms', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateRoom: (
    id: string,
    data: {
      name: string;
      roomNumber?: string;
      maxWatchingCapacity: number;
      isActive: boolean;
      assets?: { venueAssetTypeId: string; quantity: number; workingCount: number; notes?: string }[];
    }
  ) =>
    apiFetch<import('@/types').Room>(`/assets/rooms/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteRoom: (id: string) => apiFetch<void>(`/assets/rooms/${id}`, { method: 'DELETE' }),
  getDevices: (roomId?: string) =>
    apiFetch<import('@/types').Device[]>(
      `/assets/devices${roomId ? `?roomId=${roomId}` : ''}`
    ),
  createDevice: (data: {
    roomId?: string | null;
    name: string;
    identifier?: string;
    controllers?: { controllerTypeId: string; quantity: number; workingCount: number }[];
    screen?: { count: number; workingCount: number; notes?: string };
  }) =>
    apiFetch<import('@/types').Device>('/assets/devices', {
      method: 'POST',
      body: JSON.stringify({ ...data, roomId: data.roomId || null }),
    }),
  updateDevice: (
    id: string,
    data: {
      roomId?: string | null;
      name: string;
      isActive: boolean;
      identifier?: string;
      controllers?: { controllerTypeId: string; quantity: number; workingCount: number }[];
      screen?: { count: number; workingCount: number; notes?: string };
    }
  ) =>
    apiFetch<import('@/types').Device>(`/assets/devices/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ ...data, roomId: data.roomId || null }),
    }),
  deleteDevice: (id: string) => apiFetch<void>(`/assets/devices/${id}`, { method: 'DELETE' }),
  getControllerTypes: () =>
    apiFetch<import('@/types').ControllerType[]>('/assets/controller-types'),
  createControllerType: (data: { name: string; description?: string }) =>
    apiFetch<import('@/types').ControllerType>('/assets/controller-types', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateControllerType: (id: string, data: { name: string; description?: string; isActive: boolean }) =>
    apiFetch<import('@/types').ControllerType>(`/assets/controller-types/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteControllerType: (id: string) =>
    apiFetch<void>(`/assets/controller-types/${id}`, { method: 'DELETE' }),
  getVenueAssetTypes: () =>
    apiFetch<import('@/types').VenueAssetType[]>('/assets/venue-asset-types'),
  createVenueAssetType: (data: {
    name: string;
    description?: string;
    totalQuantity: number;
    workingCount: number;
  }) =>
    apiFetch<import('@/types').VenueAssetType>('/assets/venue-asset-types', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateVenueAssetType: (
    id: string,
    data: {
      name: string;
      description?: string;
      totalQuantity: number;
      workingCount: number;
      isActive: boolean;
    }
  ) =>
    apiFetch<import('@/types').VenueAssetType>(`/assets/venue-asset-types/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteVenueAssetType: (id: string) =>
    apiFetch<void>(`/assets/venue-asset-types/${id}`, { method: 'DELETE' }),
};

export const pricingApi = {
  getPlans: (mode?: number) =>
    apiFetch<import('@/types').PricingPlan[]>(
      `/pricing/plans${mode ? `?mode=${mode}` : ''}`
    ),
  createPlan: (data: {
    name: string;
    sessionMode: number;
    timeUnit: number;
    watchingBilling?: number;
    vipSurchargePerHour?: number;
    gamingRates?: { controllerCount: number; rate: number }[];
    watchingRates?: { ratePerPerson: number }[];
    packageDurationMinutes?: number | null;
    packagePrice?: number | null;
  }) =>
    apiFetch<import('@/types').PricingPlan>('/pricing/plans', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updatePlan: (
    id: string,
    data: {
      name: string;
      sessionMode: number;
      timeUnit: number;
      watchingBilling?: number;
      vipSurchargePerHour?: number;
      isActive: boolean;
      gamingRates?: { controllerCount: number; rate: number }[];
      watchingRates?: { ratePerPerson: number }[];
      packageDurationMinutes?: number | null;
      packagePrice?: number | null;
    }
  ) =>
    apiFetch<import('@/types').PricingPlan>(`/pricing/plans/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deletePlan: (id: string) => apiFetch<void>(`/pricing/plans/${id}`, { method: 'DELETE' }),
};

export const notificationsApi = {
  getUnreadCount: () => apiFetch<{ count: number }>('/notifications/unread-count'),
};

export const cafeteriaApi = {
  getItems: (opts?: { kind?: number; forSaleOnly?: boolean }) => {
    const params = new URLSearchParams();
    if (opts?.kind != null) params.set('kind', String(opts.kind));
    if (opts?.forSaleOnly) params.set('forSaleOnly', 'true');
    const q = params.toString();
    return apiFetch<import('@/types').CafeteriaItem[]>(`/cafeteria/items${q ? `?${q}` : ''}`);
  },
  createItem: (data: {
    name: string;
    kind: number;
    nameAr?: string;
    currentQuantity?: number;
    minThreshold?: number;
    sellPrice?: number;
    baseUnitId?: string;
    largeUnitId?: string;
    unitsPerLarge?: number;
    initialStockUnit?: number;
    variants?: {
      id?: string;
      name: string;
      sellPrice: number;
      isActive?: boolean;
      sortOrder?: number;
      recipeLines?: { id?: string; warehouseItemId: string; quantity: number }[];
    }[];
  }) =>
    apiFetch<import('@/types').CafeteriaItem>('/cafeteria/items', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateItem: (
    id: string,
    data: {
      name: string;
      kind: number;
      nameAr?: string;
      minThreshold: number;
      isActive: boolean;
      sellPrice?: number;
      baseUnitId?: string;
      largeUnitId?: string;
      unitsPerLarge?: number;
      variants?: {
        id?: string;
        name: string;
        sellPrice: number;
        isActive?: boolean;
        sortOrder?: number;
        recipeLines?: { id?: string; warehouseItemId: string; quantity: number }[];
      }[];
    }
  ) =>
    apiFetch<import('@/types').CafeteriaItem>(`/cafeteria/items/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteItem: (id: string) =>
    apiFetch<void>(`/cafeteria/items/${id}`, { method: 'DELETE' }),
  getAddOns: (activeOnly = false) =>
    apiFetch<import('@/types').CafeteriaAddOn[]>(
      `/cafeteria/addons${activeOnly ? '?activeOnly=true' : ''}`
    ),
  createAddOn: (data: {
    name: string;
    sellPrice: number;
    warehouseItemId: string;
    deductQuantity: number;
  }) =>
    apiFetch<import('@/types').CafeteriaAddOn>('/cafeteria/addons', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateAddOn: (
    id: string,
    data: {
      name: string;
      sellPrice: number;
      warehouseItemId: string;
      deductQuantity: number;
      isActive: boolean;
    }
  ) =>
    apiFetch<import('@/types').CafeteriaAddOn>(`/cafeteria/addons/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteAddOn: (id: string) =>
    apiFetch<void>(`/cafeteria/addons/${id}`, { method: 'DELETE' }),
  getSales: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const q = params.toString();
    return apiFetch<import('@/types').CafeteriaSale[]>(`/cafeteria/sales${q ? `?${q}` : ''}`);
  },
  createSale: (
    lines: {
      cafeteriaItemId: string;
      variantId: string;
      quantity: number;
      stockDeductQuantity?: number;
      unit?: number;
      addOns?: { addOnId: string; quantity: number }[];
    }[],
    payment: import('@/types').PaymentRequest,
    customerName?: string,
    allowSkipMissingIngredients?: boolean
  ) =>
    apiFetch<import('@/types').CafeteriaSale>('/cafeteria/sales', {
      method: 'POST',
      body: JSON.stringify({
        lines,
        payment,
        customerName: customerName || undefined,
        allowSkipMissingIngredients: allowSkipMissingIngredients || undefined,
      }),
    }),
  getOpenHolds: () => apiFetch<import('@/types').CafeteriaHold[]>('/cafeteria/holds'),
  createHold: (
    lines: {
      cafeteriaItemId: string;
      variantId: string;
      quantity: number;
      stockDeductQuantity?: number;
      unit?: number;
      addOns?: { addOnId: string; quantity: number }[];
    }[],
    opts?: { guestName?: string; customerId?: string; allowSkipMissingIngredients?: boolean }
  ) =>
    apiFetch<import('@/types').CafeteriaHold>('/cafeteria/holds', {
      method: 'POST',
      body: JSON.stringify({
        lines,
        guestName: opts?.guestName,
        customerId: opts?.customerId,
        allowSkipMissingIngredients: opts?.allowSkipMissingIngredients || undefined,
      }),
    }),
  attachHoldToSession: (holdId: string, sessionId: string) =>
    apiFetch<import('@/types').CafeteriaHold>(`/cafeteria/holds/${holdId}/attach-session`, {
      method: 'POST',
      body: JSON.stringify({ sessionId }),
    }),
  convertHoldToSale: (
    holdId: string,
    payment: import('@/types').PaymentRequest,
    customerName?: string
  ) =>
    apiFetch<import('@/types').CafeteriaSale>(`/cafeteria/holds/${holdId}/convert-sale`, {
      method: 'POST',
      body: JSON.stringify({ payment, customerName: customerName || undefined }),
    }),
  cancelHold: (holdId: string) =>
    apiFetch<import('@/types').CafeteriaHold>(`/cafeteria/holds/${holdId}/cancel`, {
      method: 'POST',
    }),
};

export const inventoryApi = {
  getMovements: (itemId?: string, page = 1, pageSize = 20) => {
    const params = new URLSearchParams();
    if (itemId) params.set('itemId', itemId);
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return apiFetch<import('@/types').PagedResult<import('@/types').InventoryMovement>>(
      `/inventory/movements?${params}`
    );
  },
  adjust: (itemId: string, newQuantity: number, reason: string) =>
    apiFetch<import('@/types').CafeteriaItem>(`/inventory/items/${itemId}/adjust`, {
      method: 'POST',
      body: JSON.stringify({ newQuantity, reason }),
    }),
  getVouchers: (type?: number, page = 1, pageSize = 20) => {
    const params = new URLSearchParams();
    if (type != null) params.set('type', String(type));
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return apiFetch<import('@/types').PagedResult<import('@/types').StockVoucher>>(
      `/inventory/vouchers?${params}`
    );
  },
  createVoucher: (data: {
    voucherType: number;
    lines: { cafeteriaItemId: string; quantity: number; unit?: number; notes?: string }[];
    notes?: string;
    relatedCountVoucherId?: string;
  }) =>
    apiFetch<import('@/types').StockVoucher>('/inventory/vouchers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  postVoucher: (id: string) =>
    apiFetch<import('@/types').StockVoucher>(`/inventory/vouchers/${id}/post`, { method: 'POST' }),
  settlementFromCount: (countId: string, notes?: string) =>
    apiFetch<import('@/types').StockVoucher>(`/inventory/vouchers/${countId}/settlement`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),
  getUnits: (activeOnly = true) =>
    apiFetch<import('@/types').InventoryUnit[]>(`/inventory/units?activeOnly=${activeOnly}`),
  createUnit: (data: { name: string; nameAr?: string }) =>
    apiFetch<import('@/types').InventoryUnit>('/inventory/units', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateUnit: (id: string, data: { name: string; nameAr?: string | null; isActive: boolean }) =>
    apiFetch<import('@/types').InventoryUnit>(`/inventory/units/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteUnit: (id: string) => apiFetch<void>(`/inventory/units/${id}`, { method: 'DELETE' }),
  getConversionLogs: (itemId?: string) =>
    apiFetch<import('@/types').ItemUnitConversionLog[]>(
      `/inventory/conversion-logs${itemId ? `?itemId=${itemId}` : ''}`
    ),
};

export const accountingApi = {
  getCategories: () => apiFetch<import('@/types').ExpenseCategory[]>('/accounting/categories'),
  createCategory: (data: { name: string; nameAr?: string }) =>
    apiFetch<import('@/types').ExpenseCategory>('/accounting/categories', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateCategory: (id: string, data: { name: string; nameAr?: string; isActive: boolean }) =>
    apiFetch<import('@/types').ExpenseCategory>(`/accounting/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteCategory: (id: string) =>
    apiFetch<void>(`/accounting/categories/${id}`, { method: 'DELETE' }),
  getExpenses: (from?: string, to?: string, page = 1, pageSize = 20) => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return apiFetch<import('@/types').PagedResult<import('@/types').Expense>>(
      `/accounting/expenses?${params}`
    );
  },
  createExpense: (data: { categoryId: string; amount: number; description: string; expenseDate: string }) =>
    apiFetch<import('@/types').Expense>('/accounting/expenses', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateExpense: (
    id: string,
    data: { categoryId: string; amount: number; description: string; expenseDate: string }
  ) =>
    apiFetch<import('@/types').Expense>(`/accounting/expenses/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  deleteExpense: (id: string) =>
    apiFetch<void>(`/accounting/expenses/${id}`, { method: 'DELETE' }),
  getDashboard: (from: string, to: string, branchId?: string) => {
    const params = new URLSearchParams({ from, to });
    if (branchId) params.set('branchId', branchId);
    return apiFetch<import('@/types').FinancialDashboard>(`/accounting/dashboard?${params}`);
  },
};

export const reportsApi = {
  getRevenue: (from: string, to: string, branchId?: string) => {
    const params = new URLSearchParams({ from, to });
    if (branchId) params.set('branchId', branchId);
    return apiFetch<import('@/types').RevenueReport>(`/reports/revenue?${params}`);
  },
  getBestSellers: (from: string, to: string, top = 10) =>
    apiFetch<import('@/types').BestSeller[]>(
      `/reports/best-sellers?from=${from}&to=${to}&top=${top}`
    ),
  getDeviceUsage: (from: string, to: string) =>
    apiFetch<import('@/types').DeviceUsage[]>(`/reports/device-usage?from=${from}&to=${to}`),
  getCashDrawer: (date: string) => {
    const tzOffsetMinutes = -new Date().getTimezoneOffset();
    return apiFetch<import('@/types').CashDrawer>(
      `/reports/cash-drawer?date=${date}&tzOffsetMinutes=${tzOffsetMinutes}`
    );
  },
  collectCash: (data: { amount: number; note?: string; date: string }) => {
    const tzOffsetMinutes = -new Date().getTimezoneOffset();
    return apiFetch<import('@/types').CashDrawer>('/reports/cash-drawer/collect', {
      method: 'POST',
      body: JSON.stringify({ ...data, tzOffsetMinutes }),
    });
  },
};

export const usersApi = {
  getAll: (page = 1, pageSize = 20) =>
    apiFetch<import('@/types').PagedResult<import('@/types').ManagedUser>>(
      `/users?page=${page}&pageSize=${pageSize}`
    ),
  getPermissions: async () => {
    try {
      return await apiFetch<import('@/types').PermissionInfo[]>('/users/permissions');
    } catch {
      return [];
    }
  },
  create: (data: {
    username: string;
    password: string;
    firstName: string;
    lastName: string;
    role?: number;
    isMaster: boolean;
    subscriptionExpiresAt?: string | null;
    allowedNotificationChannels?: number;
    permissionCodes?: string[];
    branchIds?: string[];
  }) =>
    apiFetch<import('@/types').ManagedUser>('/users', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (
    id: string,
    data: {
      firstName: string;
      lastName: string;
      isActive: boolean;
      role?: number;
      isMaster: boolean;
      subscriptionExpiresAt?: string | null;
      allowedNotificationChannels?: number;
      permissionCodes?: string[];
      branchIds?: string[];
    }
  ) =>
    apiFetch<import('@/types').ManagedUser>(`/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) => apiFetch<void>(`/users/${id}`, { method: 'DELETE' }),
  resetPassword: (id: string, newPassword: string) =>
    apiFetch<import('@/types').ResetPasswordResult>(`/users/${id}/reset-password`, {
      method: 'POST',
      body: JSON.stringify({ newPassword }),
    }),
};

export const alertsApi = {
  getSettings: () => apiFetch<import('@/types').MasterAlertSettings>('/alerts/settings'),
  saveSettings: (data: {
    smtpHost?: string;
    smtpPort: number;
    smtpUsername?: string;
    smtpPassword?: string;
    senderDisplayName?: string;
    alertRecipientEmail?: string;
    ownerWhatsAppPhone?: string;
    notifyLowStock: boolean;
    notifySubscription: boolean;
    notifyDeviceMaintenance: boolean;
  }) =>
    apiFetch<import('@/types').MasterAlertSettings>('/alerts/settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  testEmail: () =>
    apiFetch<{ message: string }>('/alerts/settings/test-email', { method: 'POST' }),
  getMaintenance: () =>
    apiFetch<import('@/types').DeviceMaintenance[]>('/alerts/maintenance'),
  startMaintenance: (data: { deviceId: string; reason: string; notes?: string }) =>
    apiFetch<import('@/types').DeviceMaintenance>('/alerts/maintenance', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  completeMaintenance: (id: string, notes?: string) =>
    apiFetch<import('@/types').DeviceMaintenance>(`/alerts/maintenance/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),
  downloadInvoicePdf: async (sessionId: string) => {
    const { accessToken, activeBranchId } = useAuthStore.getState();
    const headers: Record<string, string> = {};
    if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
    if (activeBranchId) headers['X-Branch-Id'] = activeBranchId;
    const res = await fetch(`${API_BASE}/alerts/invoices/${sessionId}/pdf`, { headers });
    if (!res.ok) throw new Error('Failed to download PDF');
    return res.blob();
  },
};

export const auditApi = {
  getLogs: (params: {
    page?: number;
    pageSize?: number;
    userId?: string;
    branchId?: string;
    actionType?: string;
    from?: string;
    to?: string;
  }) => {
    const qs = new URLSearchParams();
    if (params.page) qs.set('page', String(params.page));
    if (params.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params.userId) qs.set('userId', params.userId);
    if (params.branchId) qs.set('branchId', params.branchId);
    if (params.actionType) qs.set('actionType', params.actionType);
    if (params.from) qs.set('from', params.from);
    if (params.to) qs.set('to', params.to);
    return apiFetch<import('@/types').PagedResult<import('@/types').AuditLogEntry>>(
      `/audit?${qs.toString()}`
    );
  },
};

export const branchesApi = {
  getAll: () => apiFetch<import('@/types').BranchDetail[]>('/branches'),
  getById: (id: string) => apiFetch<import('@/types').BranchDetail>(`/branches/${id}`),
  create: (data: { name: string; address?: string; phone?: string; invoicePrefix?: string }) =>
    apiFetch<import('@/types').BranchDetail>('/branches', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (
    id: string,
    data: {
      name: string;
      address?: string;
      phone?: string;
      invoicePrefix?: string;
      isActive: boolean;
      paymentAccounts?: {
        accountType: number;
        label?: string | null;
        accountNumber: string;
        sortOrder?: number;
        isActive?: boolean;
      }[];
    }
  ) =>
    apiFetch<import('@/types').BranchDetail>(`/branches/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) => apiFetch<void>(`/branches/${id}`, { method: 'DELETE' }),
};

export const uploadsApi = {
  paymentProof: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return apiFetch<{ url: string; fileName: string; contentType: string }>('/uploads/payment-proof', {
      method: 'POST',
      body: form,
    });
  },
};

export const customersApi = {
  getAll: (q?: string, page = 1, pageSize = 20) => {
    const params = new URLSearchParams();
    if (q?.trim()) params.set('q', q.trim());
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return apiFetch<import('@/types').PagedResult<import('@/types').Customer>>(
      `/customers?${params}`
    );
  },
  getById: (id: string) => apiFetch<import('@/types').Customer>(`/customers/${id}`),
  create: (data: { name: string; phone: string; notes?: string; isActive?: boolean }) =>
    apiFetch<import('@/types').Customer>('/customers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (
    id: string,
    data: { name: string; phone: string; notes?: string | null; isActive: boolean }
  ) =>
    apiFetch<import('@/types').Customer>(`/customers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) => apiFetch<void>(`/customers/${id}`, { method: 'DELETE' }),
  topUpWallet: (id: string, data: { amount: number; bonusAmount?: number; note?: string }) =>
    apiFetch<import('@/types').Customer>(`/customers/${id}/wallet/topup`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  getWalletTransactions: (id: string, page = 1, pageSize = 20) =>
    apiFetch<import('@/types').PagedResult<import('@/types').WalletTransaction>>(
      `/customers/${id}/wallet?page=${page}&pageSize=${pageSize}`
    ),
};

export const offersApi = {
  getAll: (activeOnly?: boolean) => {
    const params = new URLSearchParams();
    if (activeOnly != null) params.set('activeOnly', String(activeOnly));
    const q = params.toString();
    return apiFetch<import('@/types').CustomerOffer[]>(`/offers${q ? `?${q}` : ''}`);
  },
  create: (data: { title: string; message: string; isActive?: boolean }) =>
    apiFetch<import('@/types').CustomerOffer>('/offers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (id: string, data: { title: string; message: string; isActive: boolean }) =>
    apiFetch<import('@/types').CustomerOffer>(`/offers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) => apiFetch<void>(`/offers/${id}`, { method: 'DELETE' }),
};

export const whatsappApi = {
  status: () => apiFetch<import('@/types').WhatsAppStatus>('/whatsapp/status'),
  qr: () => apiFetch<import('@/types').WhatsAppQr>('/whatsapp/qr'),
  saveSession: (data: { sessionId: string; phone?: string }) =>
    apiFetch<import('@/types').WhatsAppStatus>('/whatsapp/session', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  disconnect: () =>
    apiFetch<import('@/types').WhatsAppStatus>('/whatsapp/disconnect', { method: 'POST' }),
  send: (data: { phone: string; message: string }) =>
    apiFetch<import('@/types').SendWhatsAppResult>('/whatsapp/send', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  sendInvoice: (sessionId: string) =>
    apiFetch<import('@/types').SendWhatsAppResult>(`/whatsapp/send-invoice/${sessionId}`, {
      method: 'POST',
    }),
  sendOffer: (data: { customerId: string; offerId: string }) =>
    apiFetch<import('@/types').SendWhatsAppResult>('/whatsapp/send-offer', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};
