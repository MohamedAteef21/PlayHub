using PlayHub.Domain.Common;

namespace PlayHub.Domain.Entities;

public class Customer : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    /// <summary>Auto-generated code e.g. C00001.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Digits-only normalized phone.</summary>
    public string Phone { get; set; } = string.Empty;
    public string? Notes { get; set; }
    /// <summary>Prepaid wallet credit (top-ups + bonuses − payments).</summary>
    public decimal WalletBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];
}

public enum WalletTransactionType : short
{
    TopUp = 1,
    /// <summary>Free credit granted with a top-up (e.g. pay 500 get 550). Logged separately for accounting.</summary>
    Bonus = 2,
    Payment = 3,
    Adjustment = 4
}

/// <summary>Ledger of every wallet credit/debit. Amount is signed (+credit / −debit).</summary>
public class WalletTransaction : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Branch whose till physically received the money (top-ups). Null for older rows / non-cash entries.</summary>
    public Guid? BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Note { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Invoice? Invoice { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class CustomerOffer : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
