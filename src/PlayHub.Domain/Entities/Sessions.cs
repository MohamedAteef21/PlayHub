using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

public class Session : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid RoomId { get; set; }
    public SessionMode SessionMode { get; set; }
    public Guid PricingPlanId { get; set; }
    public int? ControllerCount { get; set; }
    public int? WatcherCount { get; set; }
    public string RateSnapshot { get; set; } = "{}";
    public SessionStatus Status { get; set; } = SessionStatus.Open;
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Original open time before any mode convert (null = never converted).</summary>
    public DateTime? OriginalStartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int TotalPausedSeconds { get; set; }
    /// <summary>When set, session is a fixed booking (e.g. 2 hours). Null = open-ended timer.</summary>
    public int? PlannedDurationMinutes { get; set; }
    /// <summary>Frozen time cost from previous billing segments (e.g. watching before convert to gaming).</summary>
    public decimal AccruedTimeCost { get; set; }
    public decimal TimeCost { get; set; }
    /// <summary>VIP room hourly surcharge captured at session open (rate changes don't affect open sessions).</summary>
    public decimal RoomSurchargePerHour { get; set; }
    /// <summary>Room surcharge billed as its own invoice line (separate from device time cost).</summary>
    public decimal RoomSurchargeCost { get; set; }
    public decimal CafeteriaCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TotalCost { get; set; }
    public Guid? CustomerId { get; set; }
    public string? QuickGuestName { get; set; }
    public bool IsQuickGuest { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Device Device { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public PricingPlan PricingPlan { get; set; } = null!;
    public User OpenedByUser { get; set; } = null!;
    public User? ClosedByUser { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<SessionPause> Pauses { get; set; } = [];
    public ICollection<SessionCafeteriaLine> CafeteriaLines { get; set; } = [];
    public Invoice? Invoice { get; set; }
}

public class SessionPause : BaseEntity
{
    public Guid SessionId { get; set; }
    public DateTime PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }
    public Guid PausedByUserId { get; set; }

    public Session Session { get; set; } = null!;
    public User PausedByUser { get; set; } = null!;
}

public class SessionCafeteriaLine : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid CafeteriaItemId { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? CustomerName { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
    public CafeteriaItem CafeteriaItem { get; set; } = null!;
    public User AddedByUser { get; set; } = null!;
    public ICollection<SessionCafeteriaReturn> Returns { get; set; } = [];
}

public class SessionCafeteriaReturn : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid SessionCafeteriaLineId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ReturnedByUserId { get; set; }
    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
    public SessionCafeteriaLine SessionCafeteriaLine { get; set; } = null!;
    public User ReturnedByUser { get; set; } = null!;
}
