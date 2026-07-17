using PlayHub.Domain.Common;

namespace PlayHub.Domain.Entities;

public class Room : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public int MaxWatchingCapacity { get; set; }
    /// <summary>Legacy column — VIP surcharge now lives on PricingPlan.VipSurchargePerHour.</summary>
    public decimal VipSurchargePerHour { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = [];
    public ICollection<RoomAsset> RoomAssets { get; set; } = [];
}

/// <summary>Per-master catalog for venue furniture/equipment (couches, extra TVs, etc.). Add stock here, then assign to rooms.</summary>
public class VenueAssetType : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    /// <summary>Master Admin who owns this catalog row. Staff inherit their master's id.</summary>
    public Guid? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Total stock available to assign across rooms.</summary>
    public int TotalQuantity { get; set; }
    /// <summary>How many of the total are currently working / in service.</summary>
    public int WorkingCount { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<RoomAsset> RoomAssets { get; set; } = [];
}

/// <summary>Quantity of a venue asset type assigned to a room.</summary>
public class RoomAsset : BaseEntity
{
    public Guid RoomId { get; set; }
    public Guid VenueAssetTypeId { get; set; }
    public int Quantity { get; set; }
    public int WorkingCount { get; set; }
    public string? Notes { get; set; }

    public Room Room { get; set; } = null!;
    public VenueAssetType VenueAssetType { get; set; } = null!;
}

public class Device : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    /// <summary>Optional — devices can stand alone without a room.</summary>
    public Guid? RoomId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Room? Room { get; set; }
    public Branch Branch { get; set; } = null!;
    public ICollection<DeviceController> DeviceControllers { get; set; } = [];
    public ICollection<Screen> Screens { get; set; } = [];
    public ICollection<DevicePricingPlan> DevicePricingPlans { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}

public class ControllerType : BaseEntity, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    /// <summary>Master Admin who owns this catalog row.</summary>
    public Guid? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<DeviceController> DeviceControllers { get; set; } = [];
}

public class DeviceController : BaseEntity
{
    public Guid DeviceId { get; set; }
    public Guid ControllerTypeId { get; set; }
    public int Quantity { get; set; }
    public int WorkingCount { get; set; }

    public Device Device { get; set; } = null!;
    public ControllerType ControllerType { get; set; } = null!;
}

public class Screen : BaseEntity
{
    public Guid DeviceId { get; set; }
    public int Count { get; set; }
    public int WorkingCount { get; set; }
    public string? Notes { get; set; }

    public Device Device { get; set; } = null!;
}

public class DevicePricingPlan
{
    public Guid DeviceId { get; set; }
    public Guid PricingPlanId { get; set; }
    public Enums.SessionMode SessionMode { get; set; }

    public Device Device { get; set; } = null!;
    public PricingPlan PricingPlan { get; set; } = null!;
}
