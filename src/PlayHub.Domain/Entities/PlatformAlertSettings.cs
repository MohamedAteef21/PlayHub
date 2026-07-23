using PlayHub.Domain.Common;

namespace PlayHub.Domain.Entities;

/// <summary>
/// Tenant-wide alert sender configured by Super Admin (one Gmail for the platform).
/// WhatsApp integration fields are reserved for a future provider API.
/// </summary>
public class PlatformAlertSettings : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SenderDisplayName { get; set; } = "PlayHub System";

    /// <summary>Future WhatsApp Business API / aggregator base URL.</summary>
    public string? WhatsAppIntegrationApiBaseUrl { get; set; }
    /// <summary>Future API key / token for the WhatsApp provider.</summary>
    public string? WhatsAppIntegrationApiKey { get; set; }
    public bool WhatsAppIntegrationEnabled { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
