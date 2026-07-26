using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultCurrency { get; set; } = "EGP";
    public string Timezone { get; set; } = "Africa/Cairo";
    /// <summary>When true, time is billed in whole units (round up). Default: exact prorated minutes.</summary>
    public bool BillingRoundUp { get; set; } = false;
    public bool IsActive { get; set; } = true;

    /// <summary>Optional override for WhatsApp gateway base URL; null uses appsettings.</summary>
    public string? WhatsAppApiBaseUrl { get; set; }
    public string? WhatsAppSessionId { get; set; }
    public string? WhatsAppConnectedPhone { get; set; }
    public DateTime? WhatsAppConnectedAt { get; set; }
    public int NextCustomerNumber { get; set; } = 1;

    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Customer> Customers { get; set; } = [];
    public ICollection<CustomerOffer> CustomerOffers { get; set; } = [];
    public ICollection<LoyaltyOffer> LoyaltyOffers { get; set; } = [];
}

public class Branch : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string InvoicePrefix { get; set; } = "INV";
    public int NextInvoiceNumber { get; set; } = 1;
    public int NextStockVoucherNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    /// <summary>Master user who owns / created this branch (for display).</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>When true, SharedTransferAccount is shown for both bank transfer and wallet.</summary>
    public bool UseSharedTransferAccount { get; set; }
    public string? SharedTransferAccount { get; set; }
    public string? BankTransferAccount { get; set; }
    public string? DigitalWalletAccount { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<UserBranch> UserBranches { get; set; } = [];
    public ICollection<Room> Rooms { get; set; } = [];
    public ICollection<BranchPaymentAccount> PaymentAccounts { get; set; } = [];
}

/// <summary>Bank transfer or wallet destination numbers for a branch (many allowed).</summary>
public class BranchPaymentAccount : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public PaymentAccountType AccountType { get; set; }
    /// <summary>Optional label e.g. InstaPay, Vodafone Cash, CIB.</summary>
    public string? Label { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
}

public class User : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>True for SuperAdmin and MasterAdmin — bypasses permission checks.</summary>
    public bool IsMaster { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    /// <summary>Staff belong to the Master Admin who created them.</summary>
    public Guid? ParentUserId { get; set; }
    public string? PreferredLanguage { get; set; }
    /// <summary>UI theme preference: "dark" or "light".</summary>
    public string? PreferredTheme { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    /// <summary>UTC calendar date after which the account is locked. Null = no expiry.</summary>
    public DateTime? SubscriptionExpiresAt { get; set; }
    public DateTime? SubscriptionLockedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Channels Super Admin allows this master to use for venue alerts (Email / WhatsApp / both).
    /// Staff ignore this; masters default to Email|WhatsApp.
    /// </summary>
    public NotificationChannel AllowedNotificationChannels { get; set; } =
        NotificationChannel.Email | NotificationChannel.WhatsApp;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public Tenant Tenant { get; set; } = null!;
    public User? ParentUser { get; set; }
    public ICollection<User> ChildUsers { get; set; } = [];
    public ICollection<UserBranch> UserBranches { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public MasterAlertSettings? AlertSettings { get; set; }

    public void ApplyRole(UserRole role)
    {
        Role = role;
        IsMaster = role is UserRole.SuperAdmin or UserRole.MasterAdmin;
        if (role is not UserRole.Staff)
            ParentUserId = null;
    }
}

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;

    public ICollection<UserPermission> UserPermissions { get; set; } = [];
}

public class UserPermission
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public class UserBranch
{
    public Guid UserId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public User User { get; set; } = null!;
}
