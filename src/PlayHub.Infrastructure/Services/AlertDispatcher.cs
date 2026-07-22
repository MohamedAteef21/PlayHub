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
            var masters = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.AlertSettings!).ThenInclude(s => s.Recipients)
                .Where(u =>
                    u.TenantId == tenantId &&
                    u.IsMaster &&
                    u.IsActive &&
                    !u.IsDeleted)
                .ToListAsync(ct);

            foreach (var master in masters)
            {
                var settings = master.AlertSettings;
                var recipients = settings?.Recipients?.ToList() ?? [];
                var matchingRecipients = recipients.Where(r => RecipientWants(type, r)).ToList();

                // In-app notification for the master when anyone cares, or subscription alerts with no settings yet.
                var shouldCreateInApp = matchingRecipients.Count > 0
                    || (settings is null && type is NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon)
                    || (settings is not null && recipients.Count == 0 && type is NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon);

                if (!shouldCreateInApp)
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

                var allowed = master.AllowedNotificationChannels;
                var body = $"{titleAr}\n{messageAr}\n\n{titleEn}\n{messageEn}";

                if (allowed.HasFlag(NotificationChannel.Email) &&
                    settings is not null &&
                    !string.IsNullOrWhiteSpace(settings.SmtpUsername) &&
                    !string.IsNullOrWhiteSpace(settings.SmtpPassword))
                {
                    foreach (var recipient in matchingRecipients)
                    {
                        try
                        {
                            await _email.SendAsync(settings, recipient.Email, titleAr, body, ct: ct);
                        }
                        catch
                        {
                            // ignore email failures
                        }
                    }
                }

                // WhatsApp: send if the owner phone is set and at least one recipient wants this type
                // (or subscription alerts when there are no recipients yet).
                var sendWhatsApp = matchingRecipients.Count > 0
                    || (recipients.Count == 0 && type is NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon);

                if (allowed.HasFlag(NotificationChannel.WhatsApp) &&
                    settings is not null &&
                    sendWhatsApp &&
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

    private static bool RecipientWants(NotificationType type, MasterAlertRecipient recipient) =>
        type switch
        {
            NotificationType.LowStock => recipient.NotifyLowStock,
            NotificationType.SubscriptionExpired or NotificationType.SubscriptionExpiringSoon => recipient.NotifySubscription,
            NotificationType.DeviceMaintenance or NotificationType.DeviceMaintenanceReminder => recipient.NotifyDeviceMaintenance,
            _ => true
        };
}
