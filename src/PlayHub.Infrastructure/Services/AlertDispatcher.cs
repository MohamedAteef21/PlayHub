using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.WhatsApp;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AlertDispatcher : IAlertDispatcher
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IEmailSender _email;
    private readonly IWhatsAppService _whatsApp;

    public AlertDispatcher(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IEmailSender email,
        IWhatsAppService whatsApp)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _whatsApp = whatsApp;
    }

    public async Task DispatchToMastersAsync(
        Guid tenantId,
        NotificationType type,
        string titleEn,
        string titleAr,
        string messageEn,
        string messageAr,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken ct = default)
    {
        var previousTenant = _tenantContext.TenantId;
        _tenantContext.TenantId = tenantId;

        try
        {
            var platform = await _db.PlatformAlertSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            var masters = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.AlertSettings)
                .Where(u =>
                    u.TenantId == tenantId &&
                    u.IsMaster &&
                    u.Role != UserRole.SuperAdmin &&
                    u.IsActive &&
                    !u.IsDeleted)
                .ToListAsync(ct);

            foreach (var master in masters)
            {
                var settings = master.AlertSettings;
                if (!ShouldNotify(type, settings))
                    continue;

                var recentExists = relatedEntityId.HasValue && await _db.Notifications.IgnoreQueryFilters().AnyAsync(n =>
                    n.UserId == master.Id &&
                    n.Type == type &&
                    n.RelatedEntityId == relatedEntityId &&
                    n.CreatedAt > DateTime.UtcNow.AddHours(-24), ct);

                if (recentExists) continue;

                _db.Notifications.Add(new Notification
                {
                    TenantId = tenantId,
                    UserId = master.Id,
                    Type = type,
                    Title = titleEn,
                    TitleAr = titleAr,
                    Message = messageEn,
                    MessageAr = messageAr,
                    RelatedEntityType = relatedEntityType,
                    RelatedEntityId = relatedEntityId
                });

                var body = $"{titleAr}\n{messageAr}\n\n{titleEn}\n{messageEn}";

                // Platform Gmail (Super Admin) is the single sender when configured.
                var recipient = settings?.AlertRecipientEmail;
                if (string.IsNullOrWhiteSpace(recipient))
                    recipient = master.Email;

                var canEmail = !string.IsNullOrWhiteSpace(recipient) &&
                    (
                        (platform is not null &&
                         !string.IsNullOrWhiteSpace(platform.SmtpUsername) &&
                         !string.IsNullOrWhiteSpace(platform.SmtpPassword))
                        ||
                        (settings is not null &&
                         !string.IsNullOrWhiteSpace(settings.SmtpUsername) &&
                         !string.IsNullOrWhiteSpace(settings.SmtpPassword))
                    );

                if (canEmail)
                {
                    try
                    {
                        if (platform is not null &&
                            !string.IsNullOrWhiteSpace(platform.SmtpUsername) &&
                            !string.IsNullOrWhiteSpace(platform.SmtpPassword))
                        {
                            await _email.SendWithCredentialsAsync(
                                platform.SmtpUsername,
                                platform.SmtpPassword,
                                platform.SenderDisplayName,
                                recipient!,
                                titleAr,
                                body,
                                ct: ct);
                        }
                        else if (settings is not null)
                        {
                            await _email.SendAsync(settings, recipient!, titleAr, body, ct: ct);
                        }
                    }
                    catch
                    {
                        // ignore email failures
                    }
                }

                // Legacy per-owner WhatsApp until platform integration ships.
                if (settings is not null &&
                    !string.IsNullOrWhiteSpace(settings.OwnerWhatsAppPhone))
                {
                    try
                    {
                        await _whatsApp.SendTextAsync(settings.OwnerWhatsAppPhone, body, ct);
                    }
                    catch
                    {
                        // ignore WA failures
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _tenantContext.TenantId = previousTenant;
        }
    }

    private static bool ShouldNotify(NotificationType type, MasterAlertSettings? settings)
    {
        if (settings is null)
            return type is NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon;

        return type switch
        {
            NotificationType.LowStock => settings.NotifyLowStock,
            NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon => settings.NotifySubscription,
            NotificationType.DeviceMaintenance or NotificationType.DeviceMaintenanceReminder => settings.NotifyDeviceMaintenance,
            _ => true
        };
    }
}
