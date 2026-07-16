using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

public class CafeteriaItem : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public decimal SellPrice { get; set; }
    /// <summary>On-hand stock always stored in base (small) units.</summary>
    public int CurrentQuantity { get; set; }
    public int MinThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Small / base unit name (e.g. piece).</summary>
    public string BaseUnitName { get; set; } = "قطعة";
    /// <summary>Optional large unit name (e.g. carton). Null = base-only.</summary>
    public string? LargeUnitName { get; set; }
    /// <summary>How many base units equal one large unit. Must be &gt;= 2 when large unit is set.</summary>
    public int UnitsPerLarge { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
}

/// <summary>Tenant-managed unit names (piece, carton, …) picked when creating items.</summary>
public class InventoryUnit : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>History of base/large unit or conversion factor changes on an item.</summary>
public class ItemUnitConversionLog : BaseEntity, ITenantEntity, IBranchEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    public string OldBaseUnitName { get; set; } = string.Empty;
    public string NewBaseUnitName { get; set; } = string.Empty;
    public string? OldLargeUnitName { get; set; }
    public string? NewLargeUnitName { get; set; }
    public int OldUnitsPerLarge { get; set; }
    public int NewUnitsPerLarge { get; set; }
    public Guid ChangedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}

public class CafeteriaSale : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid SoldByUserId { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public CafeteriaSaleStatus Status { get; set; } = CafeteriaSaleStatus.Completed;
    public DateTime SoldAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public Session? Session { get; set; }
    public User SoldByUser { get; set; } = null!;
    public ICollection<CafeteriaSaleLine> Lines { get; set; } = [];
    public ICollection<CafeteriaReturn> Returns { get; set; } = [];
    public Invoice? Invoice { get; set; }
}

public class CafeteriaSaleLine : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public CafeteriaSale Sale { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
}

public class CafeteriaReturn : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid SaleLineId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ReturnedByUserId { get; set; }
    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;

    public CafeteriaSale Sale { get; set; } = null!;
    public CafeteriaSaleLine SaleLine { get; set; } = null!;
    public User ReturnedByUser { get; set; } = null!;
}

public class InventoryMovement : BaseEntity, IBranchEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public int QuantityChange { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public Guid PerformedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
    public User PerformedByUser { get; set; } = null!;
}

public class PurchaseOrder : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string? SupplierName { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal TotalCost { get; set; }
    public DateTime? OrderedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ReceivedByUser { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
    public Expense? Expense { get; set; }
}

public class PurchaseOrderLine : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
}

/// <summary>Formal warehouse voucher: stock-in (إذن إضافة), count (جرد), or settlement (تسوية).</summary>
public class StockVoucher : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public StockVoucherType VoucherType { get; set; }
    public StockVoucherStatus Status { get; set; } = StockVoucherStatus.Draft;
    public string? Notes { get; set; }
    public Guid? RelatedCountVoucherId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? PostedByUser { get; set; }
    public StockVoucher? RelatedCountVoucher { get; set; }
    public ICollection<StockVoucherLine> Lines { get; set; } = [];
}

public class StockVoucherLine : BaseEntity
{
    public Guid StockVoucherId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    /// <summary>Always stored in base (small) units. StockIn: qty added. Count: counted qty. Settlement: signed delta.</summary>
    public int Quantity { get; set; }
    /// <summary>System qty snapshot (count / settlement from count).</summary>
    public int? SystemQuantity { get; set; }
    /// <summary>What the user typed before conversion (StockIn may be large units).</summary>
    public int? EnteredQuantity { get; set; }
    public InventoryUnitKind EnteredUnit { get; set; } = InventoryUnitKind.Base;
    public string? Notes { get; set; }

    public StockVoucher StockVoucher { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
}
