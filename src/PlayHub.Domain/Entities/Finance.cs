using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

public class Invoice : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceType InvoiceType { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? CafeteriaSaleId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal Total { get; set; }
    public InvoiceStatus Status { get; set; }
    public Guid ClosedByUserId { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public Session? Session { get; set; }
    public CafeteriaSale? CafeteriaSale { get; set; }
    public User ClosedByUser { get; set; } = null!;
    public ICollection<InvoicePayment> Payments { get; set; } = [];
    public RevenueEntry? RevenueEntry { get; set; }
}

public class InvoicePayment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? DebtorName { get; set; }
    public string? DebtorPhone { get; set; }
    /// <summary>Registered customer linked when debt is from a session/customer (for open-session warnings).</summary>
    public Guid? CustomerId { get; set; }
    public DateTime? CollectedAt { get; set; }
    public PaymentMethod? CollectionMethod { get; set; }
    public Guid? CollectedByUserId { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Customer? Customer { get; set; }
    public User? CollectedByUser { get; set; }
    public PaymentProof? Proof { get; set; }
}

public class PaymentProof : BaseEntity
{
    public Guid InvoicePaymentId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public InvoicePayment InvoicePayment { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}

public class RevenueEntry : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public RevenueType RevenueType { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
}

public class ExpenseCategory : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<Expense> Expenses { get; set; } = [];
}

public class Expense : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    public Guid RecordedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public ExpenseCategory Category { get; set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; set; }
    public User RecordedByUser { get; set; } = null!;
}

/// <summary>Cash physically taken out of the branch till by the master (full or partial settlement).</summary>
public class CashCollection : BaseEntity, IBranchEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public Guid CollectedByUserId { get; set; }
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    public Branch Branch { get; set; } = null!;
    public User CollectedByUser { get; set; } = null!;
}

public class AuditLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Details { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public bool Success { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public User User { get; set; } = null!;
}

public class Notification : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MessageAr { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}
