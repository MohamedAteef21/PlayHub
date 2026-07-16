namespace PlayHub.Domain.Enums;

/// <summary>
/// SuperAdmin creates Master Admins; Master Admin creates staff with custom permissions.
/// Both SuperAdmin and MasterAdmin set IsMaster=true (bypass permission checks).
/// </summary>
public enum UserRole : short
{
    Staff = 0,
    MasterAdmin = 1,
    SuperAdmin = 2
}

public enum SessionMode : short
{
    Gaming = 1,
    Watching = 2
}

public enum TimeUnit : short
{
    PerMinute = 1,
    PerHour = 2,
    /// <summary>Flat rate per game/match (ignore elapsed time).</summary>
    PerGame = 3
}

public enum WatchingBilling : short
{
    /// <summary>Flat fee: rate × watcher count for the whole watching session (no time).</summary>
    PerPerson = 1,
    /// <summary>Timed: rate × watcher count × time units for each person added into the room in use.</summary>
    PerScreen = 2
}

public enum SessionStatus : short
{
    Open = 1,
    Paused = 2,
    Closed = 3
}

public enum InvoiceType : short
{
    Session = 1,
    Cafeteria = 2
}

public enum InvoiceStatus : short
{
    Paid = 1,
    Deferred = 2,
    PartiallyPaid = 3
}

public enum PaymentMethod : short
{
    Cash = 1,
    BankTransfer = 2,
    DigitalWallet = 3,
    Deferred = 4,
    /// <summary>Paid from the customer's prepaid wallet balance.</summary>
    CustomerWallet = 5
}

/// <summary>Destination account type shown when closing with transfer/wallet.</summary>
public enum PaymentAccountType : short
{
    BankTransfer = 1,
    DigitalWallet = 2
}

public enum PaymentStatus : short
{
    Completed = 1,
    PendingVerification = 2,
    Deferred = 3,
    Collected = 4
}

public enum CafeteriaSaleStatus : short
{
    Completed = 1,
    PartiallyReturned = 2,
    FullyReturned = 3
}

public enum PurchaseOrderStatus : short
{
    Draft = 1,
    Ordered = 2,
    Received = 3,
    Cancelled = 4
}

public enum InventoryMovementType : short
{
    Sale = 1,
    Return = 2,
    PurchaseReceive = 3,
    ManualAdjust = 4,
    InitialStock = 5,
    StockIn = 6,
    StockCount = 7,
    Settlement = 8
}

/// <summary>Dispense / stock-entry unit. Stock is always stored in Base units.</summary>
public enum InventoryUnitKind : short
{
    Base = 0,
    Large = 1
}

public enum StockVoucherType : short
{
    StockIn = 1,
    StockCount = 2,
    Settlement = 3
}

public enum StockVoucherStatus : short
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public enum RevenueType : short
{
    Session = 1,
    Cafeteria = 2
}

public enum NotificationType : short
{
    LowStock = 1,
    OverdueReceivable = 2,
    SecurityAlert = 3,
    SubscriptionExpired = 4,
    /// <summary>Sent ~7 days before subscription end date.</summary>
    SubscriptionExpiringSoon = 5,
    DeviceMaintenance = 6,
    DeviceMaintenanceReminder = 7
}

/// <summary>Channels a master may use for outbound alerts (Super Admin grants these).</summary>
[Flags]
public enum NotificationChannel : short
{
    None = 0,
    Email = 1,
    WhatsApp = 2
}
