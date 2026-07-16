using PlayHub.Domain.Common;

namespace PlayHub.Domain.Entities;

/// <summary>Per-master Gmail + WhatsApp alert routing for the venue.</summary>
public class MasterAlertSettings : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SMTP host, e.g. smtp.gmail.com</summary>
    public string? SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    /// <summary>Sender Gmail address (SMTP username).</summary>
    public string? SmtpUsername { get; set; }
    /// <summary>Gmail App Password (not the normal password).</summary>
    public string? SmtpPassword { get; set; }
    public string? SenderDisplayName { get; set; }

    /// <summary>Where alert emails are delivered (venue inbox).</summary>
    public string? AlertRecipientEmail { get; set; }
    /// <summary>Venue owner WhatsApp number for important updates.</summary>
    public string? OwnerWhatsAppPhone { get; set; }

    public bool NotifyLowStock { get; set; } = true;
    public bool NotifySubscription { get; set; } = true;
    public bool NotifyDeviceMaintenance { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>Device taken out for repair / maintenance with a reason.</summary>
public class DeviceMaintenance : BaseEntity, IBranchEntity, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Guid ReportedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public bool IsOpen => CompletedAt is null;

    public Device Device { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public User ReportedByUser { get; set; } = null!;
}
