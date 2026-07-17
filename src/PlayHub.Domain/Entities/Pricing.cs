using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

public class PricingPlan : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SessionMode SessionMode { get; set; }
    public TimeUnit TimeUnit { get; set; }
    /// <summary>Only used for watching plans. PerPerson = flat per watcher; PerScreen = timed per person added to the room.</summary>
    public WatchingBilling WatchingBilling { get; set; } = WatchingBilling.PerPerson;
    /// <summary>Package/bundle: flat price covering this many minutes. Overage billed at the plan's normal rate.</summary>
    public int? PackageDurationMinutes { get; set; }
    /// <summary>Flat package price (usually cheaper than duration × hourly rate).</summary>
    public decimal? PackagePrice { get; set; }
    /// <summary>Extra VIP amount added per billable hour when this plan is used (0 = none).</summary>
    public decimal VipSurchargePerHour { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ICollection<GamingRate> GamingRates { get; set; } = [];
    public ICollection<WatchingRate> WatchingRates { get; set; } = [];
}

public class GamingRate : BaseEntity
{
    public Guid PricingPlanId { get; set; }
    public int ControllerCount { get; set; }
    public decimal Rate { get; set; }

    public PricingPlan PricingPlan { get; set; } = null!;
}

public class WatchingRate : BaseEntity
{
    public Guid PricingPlanId { get; set; }
    public decimal RatePerPerson { get; set; }

    public PricingPlan PricingPlan { get; set; } = null!;
}
