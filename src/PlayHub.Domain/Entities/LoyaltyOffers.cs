using PlayHub.Domain.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Domain.Entities;

/// <summary>
/// Flexible venue loyalty offer: play hours/matches or buy cafeteria items → free hours/matches/items.
/// Separate from <see cref="CustomerOffer"/> (WhatsApp marketing templates).
/// </summary>
public class LoyaltyOffer : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Null = open-ended start.</summary>
    public DateTime? StartsAt { get; set; }
    /// <summary>Null = open-ended end.</summary>
    public DateTime? EndsAt { get; set; }
    public LoyaltyPlayerScope PlayerScope { get; set; } = LoyaltyPlayerScope.Any;
    public LoyaltyFulfillment Fulfillment { get; set; } = LoyaltyFulfillment.EarnCredit;
    public LoyaltyConditionLogic ConditionLogic { get; set; } = LoyaltyConditionLogic.All;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<LoyaltyOfferCondition> Conditions { get; set; } = [];
    public ICollection<LoyaltyOfferReward> Rewards { get; set; } = [];
    public ICollection<LoyaltyOfferDevice> Devices { get; set; } = [];
}

public class LoyaltyOfferCondition : BaseEntity
{
    public Guid OfferId { get; set; }
    public LoyaltyConditionMetric Metric { get; set; }
    /// <summary>Required hours, matches, or cafeteria quantity.</summary>
    public decimal RequiredQuantity { get; set; }
    /// <summary>Only for <see cref="LoyaltyConditionMetric.PlayHoursInDays"/>.</summary>
    public int? WindowDays { get; set; }
    public Guid? CafeteriaItemId { get; set; }
    public Guid? VariantId { get; set; }

    public LoyaltyOffer Offer { get; set; } = null!;
    public CafeteriaItem? CafeteriaItem { get; set; }
    public CafeteriaItemVariant? Variant { get; set; }
}

public class LoyaltyOfferReward : BaseEntity
{
    public Guid OfferId { get; set; }
    public LoyaltyRewardMetric Metric { get; set; }
    public decimal Quantity { get; set; }
    public Guid? CafeteriaItemId { get; set; }
    public Guid? VariantId { get; set; }

    public LoyaltyOffer Offer { get; set; } = null!;
    public CafeteriaItem? CafeteriaItem { get; set; }
    public CafeteriaItemVariant? Variant { get; set; }
}

/// <summary>Empty list = offer applies to all devices.</summary>
public class LoyaltyOfferDevice
{
    public Guid OfferId { get; set; }
    public Guid DeviceId { get; set; }

    public LoyaltyOffer Offer { get; set; } = null!;
    public Device Device { get; set; } = null!;
}

/// <summary>Banked loyalty reward for a customer (hours / matches / free cafeteria item).</summary>
public class LoyaltyCredit : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OfferId { get; set; }
    public Guid? SourceSessionId { get; set; }
    public LoyaltyRewardMetric RewardMetric { get; set; }
    public decimal QuantityOriginal { get; set; }
    public decimal QuantityRemaining { get; set; }
    public Guid? CafeteriaItemId { get; set; }
    public Guid? VariantId { get; set; }
    public LoyaltyCreditStatus Status { get; set; } = LoyaltyCreditStatus.Available;
    public DateTime? ExpiresAt { get; set; }
    public Guid? RedeemedOnSessionId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public LoyaltyOffer Offer { get; set; } = null!;
    public Session? SourceSession { get; set; }
    public CafeteriaItem? CafeteriaItem { get; set; }
    public CafeteriaItemVariant? Variant { get; set; }
}
