export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages?: number;
  hasNext?: boolean;
  hasPrevious?: boolean;
}

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isMaster: boolean;
  role: number;
  preferredLanguage: string | null;
  preferredTheme: string | null;
  subscriptionExpiresAt: string | null;
  permissions: string[];
  branches: Branch[];
}

export interface Branch {
  id: string;
  name: string;
  isDefault: boolean;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
  activeBranchId: string | null;
}

export interface SessionLive {
  id: string;
  branchId: string;
  deviceId: string;
  deviceName: string;
  deviceIdentifier: string;
  roomId: string | null;
  roomName: string | null;
  sessionMode: number;
  status: number;
  pricingPlanId: string;
  pricingPlanName: string;
  controllerCount: number | null;
  watcherCount: number | null;
  startedAt: string;
  originalStartedAt: string | null;
  totalPausedSeconds: number;
  pausedAt: string | null;
  elapsedSeconds: number;
  accruedTimeCost: number;
  currentTimeCost: number;
  roomSurchargeCost: number;
  cafeteriaCost: number;
  totalCost: number;
  openedByName: string;
  plannedDurationMinutes: number | null;
  remainingSeconds: number | null;
  timeExpired: boolean;
  canConvertToGaming: boolean;
  customerId: string | null;
  customerCode: string | null;
  customerName: string | null;
  customerPhone: string | null;
  isQuickGuest: boolean;
  quickGuestName: string | null;
  cafeteriaLines: SessionCafeteriaLine[];
  equipmentAllocations?: SessionEquipmentAllocation[];
}

export interface SessionEquipmentAllocation {
  branchEquipmentId: string;
  equipmentName: string;
  equipmentKind: number;
  quantity: number;
}

export interface SessionCafeteriaLine {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  variantId: string | null;
  variantName: string | null;
  quantity: number;
  stockDeductQuantity: number;
  returnedQuantity: number;
  unitPrice: number;
  lineTotal: number;
  customerName: string | null;
  addedAt: string;
  addOns: CafeteriaSaleLineAddOn[];
}

export interface CafeteriaSaleLineAddOn {
  id: string;
  addOnId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  stockDeductQuantity: number;
}

export interface SessionInvoice {
  id: string;
  invoiceNumber: string;
  total: number;
  paymentMethod: number;
  paymentStatus: number;
}

export interface SessionDetail {
  id: string;
  branchId: string;
  deviceId: string;
  deviceName: string;
  roomId: string | null;
  roomName: string | null;
  sessionMode: number;
  status: number;
  pricingPlanId: string;
  pricingPlanName: string;
  controllerCount: number | null;
  watcherCount: number | null;
  startedAt: string;
  originalStartedAt: string | null;
  closedAt: string | null;
  totalPausedSeconds: number;
  accruedTimeCost: number;
  timeCost: number;
  roomSurchargeCost: number;
  cafeteriaCost: number;
  discountAmount: number;
  discountReason: string | null;
  totalCost: number;
  openedByName: string;
  closedByName: string | null;
  plannedDurationMinutes: number | null;
  customerId: string | null;
  customerCode: string | null;
  customerName: string | null;
  customerPhone: string | null;
  isQuickGuest: boolean;
  quickGuestName: string | null;
  invoiceNumber: string | null;
  cafeteriaLines: SessionCafeteriaLine[];
  invoice: SessionInvoice | null;
}

export interface Customer {
  id: string;
  code: string;
  name: string;
  phone: string;
  notes: string | null;
  walletBalance: number;
  isActive: boolean;
  createdAt: string;
  outstandingDebtAmount: number;
  outstandingDebtCount: number;
}

export const WalletTransactionType = { TopUp: 1, Bonus: 2, Payment: 3, Adjustment: 4 } as const;

export interface WalletTransaction {
  id: string;
  type: number;
  amount: number;
  balanceAfter: number;
  note: string | null;
  createdAt: string;
}

export interface CustomerOffer {
  id: string;
  title: string;
  message: string;
  isActive: boolean;
  createdAt: string;
}

export interface WhatsAppStatus {
  ready: boolean;
  sessionId: string | null;
  phone: string | null;
  connectedAt: string | null;
}

export interface WhatsAppQr {
  qr: string | null;
  qrBase64: string | null;
  ready: boolean;
}

export interface SendWhatsAppResult {
  success: boolean;
  messageId: string | null;
  error: string | null;
}

export interface SessionHistory {
  id: string;
  deviceId: string;
  deviceName: string;
  roomName: string | null;
  sessionMode: number;
  status: number;
  startedAt: string;
  closedAt: string | null;
  openedByName: string;
  closedByName: string | null;
  timeCost: number;
  cafeteriaCost: number;
  totalCost: number;
}

export interface AssetDashboard {
  branchId: string;
  branchName: string;
  rooms: AssetDashboardRoom[];
  unassignedDevices: AssetDashboardDevice[];
  equipment: BranchEquipment[];
}

export interface BranchEquipment {
  id: string;
  branchId: string;
  name: string;
  kind: number;
  totalQuantity: number;
  maintenanceQuantity: number;
  inUseQuantity: number;
  freeQuantity: number;
  isActive: boolean;
  createdAt: string;
}

export const EquipmentKind = {
  Controller: 1,
  Paddle: 2,
  Cue: 3,
  Ball: 4,
  Other: 99,
} as const;

export interface AssetDashboardRoom {
  id: string;
  name: string;
  roomNumber: string | null;
  maxWatchingCapacity: number;
  assets: RoomAsset[];
  devices: AssetDashboardDevice[];
}

export interface RoomAsset {
  id: string;
  venueAssetTypeId: string;
  assetTypeName: string;
  quantity: number;
  workingCount: number;
  notes: string | null;
}

export interface VenueAssetType {
  id: string;
  name: string;
  description: string | null;
  totalQuantity: number;
  workingCount: number;
  assignedQuantity: number;
  isActive: boolean;
}

export interface AssetDashboardDevice {
  id: string;
  identifier: string;
  name: string;
  liveStatus: string;
  maxGamingPlayers: number;
  maxWatchingCapacity: number;
  totalControllers: number;
  workingControllers: number;
  isActive: boolean;
}

export const TimeUnit = { PerMinute: 1, PerHour: 2, PerGame: 3 } as const;
export const WatchingBilling = { PerPerson: 1, PerScreen: 2 } as const;

export interface PricingPlan {
  id: string;
  branchId: string | null;
  name: string;
  sessionMode: number;
  timeUnit: number;
  watchingBilling: number;
  vipSurchargePerHour: number;
  packageDurationMinutes: number | null;
  packagePrice: number | null;
  isActive: boolean;
  gamingRates: { controllerCount: number; rate: number }[];
  watchingRates: { ratePerPerson: number }[];
  createdAt: string;
}

export interface Notification {
  id: string;
  type: number;
  title: string;
  titleAr: string | null;
  message: string;
  messageAr: string | null;
  isRead: boolean;
  createdAt: string;
}

export const SessionMode = { Gaming: 1, Watching: 2 } as const;
export const SessionStatus = { Open: 1, Paused: 2, Closed: 3 } as const;
export const UserRole = { Staff: 0, MasterAdmin: 1, SuperAdmin: 2 } as const;
export const PaymentMethod = { Cash: 1, BankTransfer: 2, DigitalWallet: 3, Deferred: 4, CustomerWallet: 5 } as const;
export const PurchaseOrderStatus = { Draft: 1, Ordered: 2, Received: 3, Cancelled: 4 } as const;
export const InventoryMovementType = {
  Sale: 1,
  Return: 2,
  PurchaseReceive: 3,
  ManualAdjust: 4,
  InitialStock: 5,
  StockIn: 6,
  StockCount: 7,
  Settlement: 8,
} as const;

export const StockVoucherType = {
  StockIn: 1,
  StockCount: 2,
  Settlement: 3,
} as const;

export const StockVoucherStatus = {
  Draft: 1,
  Posted: 2,
  Cancelled: 3,
} as const;

export interface StockVoucherLine {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  quantity: number;
  systemQuantity: number | null;
  variance: number | null;
  enteredQuantity: number | null;
  enteredUnit: number;
  notes: string | null;
}

export interface InventoryUnit {
  id: string;
  name: string;
  nameAr: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface ItemUnitConversionLog {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  oldBaseUnitName: string;
  newBaseUnitName: string;
  oldLargeUnitName: string | null;
  newLargeUnitName: string | null;
  oldUnitsPerLarge: number;
  newUnitsPerLarge: number;
  changedByName: string;
  createdAt: string;
}

export interface StockVoucher {
  id: string;
  branchId: string;
  voucherNumber: string;
  voucherType: number;
  status: number;
  notes: string | null;
  relatedCountVoucherId: string | null;
  createdByName: string;
  createdAt: string;
  postedAt: string | null;
  postedByName: string | null;
  lines: StockVoucherLine[];
}

export interface RecipeLine {
  id: string;
  warehouseItemId: string;
  warehouseItemName: string;
  quantity: number;
  availableQuantity: number;
}

export interface CafeteriaItemVariant {
  id: string;
  name: string;
  sellPrice: number;
  isActive: boolean;
  sortOrder: number;
  recipeLines: RecipeLine[];
}

/** Matches backend CafeteriaItemKind */
export const CafeteriaItemKind = {
  Warehouse: 1,
  Menu: 2,
  SellAsIs: 3,
} as const;
export type CafeteriaItemKind = (typeof CafeteriaItemKind)[keyof typeof CafeteriaItemKind];

export interface CafeteriaItem {
  id: string;
  branchId: string;
  name: string;
  nameAr: string | null;
  sellPrice: number;
  currentQuantity: number;
  minThreshold: number;
  isLowStock: boolean;
  isActive: boolean;
  kind: CafeteriaItemKind;
  baseUnitName: string;
  largeUnitName: string | null;
  unitsPerLarge: number;
  createdAt: string;
  variants: CafeteriaItemVariant[];
}

export interface CafeteriaAddOn {
  id: string;
  branchId: string;
  name: string;
  sellPrice: number;
  warehouseItemId: string;
  warehouseItemName: string;
  deductQuantity: number;
  availableQuantity: number;
  isActive: boolean;
  createdAt: string;
}

export interface MissingIngredient {
  warehouseItemId: string;
  name: string;
  required: number;
  available: number;
}

/** Matches backend InventoryUnitKind */
export const InventoryUnitKind = {
  Base: 0,
  Large: 1,
} as const;
export type InventoryUnitKind = (typeof InventoryUnitKind)[keyof typeof InventoryUnitKind];

export interface CafeteriaSaleLine {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  variantId: string | null;
  variantName: string | null;
  quantity: number;
  stockDeductQuantity: number;
  returnedQuantity: number;
  unitPrice: number;
  lineTotal: number;
  addOns: CafeteriaSaleLineAddOn[];
}

export interface CafeteriaSale {
  id: string;
  branchId: string;
  sessionId: string | null;
  customerName: string | null;
  totalAmount: number;
  status: number;
  soldAt: string;
  soldByName: string;
  lines: CafeteriaSaleLine[];
  invoice: {
    id: string;
    invoiceNumber: string;
    total: number;
    paymentMethod: number;
    paymentStatus: number;
  } | null;
}

export interface InventoryMovement {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  movementType: number;
  quantityChange: number;
  referenceType: string | null;
  referenceId: string | null;
  notes: string | null;
  performedByName: string;
  createdAt: string;
}

export interface PurchaseOrderLine {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  orderedQuantity: number;
  receivedQuantity: number;
  unitCost: number;
  lineTotal: number;
}

export interface PurchaseOrder {
  id: string;
  branchId: string;
  supplierName: string | null;
  status: number;
  totalCost: number;
  orderedAt: string | null;
  receivedAt: string | null;
  createdByName: string;
  lines: PurchaseOrderLine[];
  expenseId: string | null;
}

export interface ExpenseCategory {
  id: string;
  name: string;
  nameAr: string | null;
  isActive: boolean;
}

export interface Expense {
  id: string;
  branchId: string;
  branchName: string;
  categoryId: string;
  categoryName: string;
  amount: number;
  description: string;
  expenseDate: string;
  purchaseOrderId: string | null;
  recordedByName: string;
  createdAt: string;
}

export interface FinancialDashboard {
  from: string;
  to: string;
  branchId: string | null;
  totalRevenue: number;
  totalExpenses: number;
  netProfit: number;
  byBranch: { branchId: string; branchName: string; revenue: number; expenses: number; netProfit: number }[];
  expensesByCategory: { categoryId: string; categoryName: string; total: number }[];
  dailyBreakdown: { date: string; revenue: number; expenses: number; netProfit: number }[];
}

export interface RevenueReport {
  from: string;
  to: string;
  branchId: string | null;
  totalRevenue: number;
  sessionRevenue: number;
  cafeteriaRevenue: number;
  daily: { date: string; sessionRevenue: number; cafeteriaRevenue: number; total: number }[];
}

export interface BestSeller {
  itemId: string;
  itemName: string;
  totalQuantity: number;
  totalRevenue: number;
}

export interface CashDrawer {
  date: string;
  branchId: string | null;
  cashSessions: number;
  cashCafeteria: number;
  cashWalletTopUps: number;
  cashCollectedDebts: number;
  totalCashIn: number;
  cashExpenses: number;
  netCash: number;
  bankTransferIn: number;
  digitalWalletIn: number;
  paidFromCustomerWallets: number;
  newDeferredDebts: number;
  collectedOnDay: number;
  drawerBalance: number;
  dayCollections: CashCollectionEntry[];
}

export interface CashCollectionEntry {
  id: string;
  amount: number;
  note: string | null;
  collectedByName: string;
  collectedAt: string;
}

export interface DeviceUsage {
  deviceId: string;
  deviceIdentifier: string;
  deviceName: string;
  roomName: string | null;
  totalHours: number;
  sessionCount: number;
}

export interface Room {
  id: string;
  branchId: string;
  name: string;
  roomNumber: string | null;
  maxWatchingCapacity: number;
  isActive: boolean;
  deviceCount: number;
  assets: RoomAsset[];
  createdAt: string;
}

export interface ControllerType {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface Device {
  id: string;
  branchId: string;
  roomId: string | null;
  roomName: string | null;
  identifier: string;
  name: string;
  isActive: boolean;
  maxGamingPlayers: number;
  maxWatchingCapacity: number;
  liveStatus: string;
  createdAt: string;
}

export interface PaymentRequest {
  paymentMethod: number;
  debtorName?: string;
  debtorPhone?: string;
  proofFileUrl?: string;
  customerId?: string;
}

export interface ResetPasswordResult {
  newPassword: string;
}

export interface CafeteriaHoldLineAddOn {
  id: string;
  addOnId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  stockDeductQuantity: number;
}

export interface CafeteriaHoldLine {
  id: string;
  cafeteriaItemId: string;
  itemName: string;
  variantId: string | null;
  variantName: string | null;
  quantity: number;
  stockDeductQuantity: number;
  unitPrice: number;
  lineTotal: number;
  addOns: CafeteriaHoldLineAddOn[];
}

export interface CafeteriaHold {
  id: string;
  branchId: string;
  guestName: string | null;
  customerId: string | null;
  customerName: string | null;
  status: number;
  totalAmount: number;
  createdAt: string;
  createdByName: string;
  attachedSessionId: string | null;
  convertedSaleId: string | null;
  finalizedAt: string | null;
  lines: CafeteriaHoldLine[];
}

export interface ManagedUser {
  id: string;
  username: string;
  firstName: string;
  lastName: string;
  isMaster: boolean;
  role: number;
  parentUserId: string | null;
  isActive: boolean;
  subscriptionExpiresAt: string | null;
  subscriptionLockedAt: string | null;
  lastLoginAt: string | null;
  allowedNotificationChannels: number;
  permissions: string[];
  branchIds: string[];
  branchNames: string[];
  createdAt: string;
}

export interface AuditLogEntry {
  id: string;
  branchId: string | null;
  branchName: string | null;
  userId: string;
  userName: string;
  actionType: string;
  entityType: string;
  entityId: string | null;
  details: string;
  success: boolean;
  timestamp: string;
}

export interface PermissionInfo {
  id: string;
  code: string;
  module: string;
  action: string;
  description: string;
}

export const PaymentAccountType = { BankTransfer: 1, DigitalWallet: 2 } as const;

export { NotificationChannel } from './alerts';
export type { MasterAlertSettings, DeviceMaintenance } from './alerts';

export interface BranchPaymentAccount {
  id: string;
  accountType: number;
  label: string | null;
  accountNumber: string;
  sortOrder: number;
  isActive: boolean;
}

export interface BranchDetail {
  id: string;
  name: string;
  address: string | null;
  phone: string | null;
  invoicePrefix: string;
  isActive: boolean;
  ownerUserId: string | null;
  ownerName: string | null;
  paymentAccounts: BranchPaymentAccount[];
  createdAt: string;
}
