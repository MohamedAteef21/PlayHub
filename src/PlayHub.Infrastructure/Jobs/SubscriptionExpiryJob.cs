using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Jobs;

/// <summary>
/// Daily:
/// 1) Warn masters ~7 days before subscription ends (in-app + email/WA via dispatcher).
/// 2) Lock expired accounts.
/// </summary>
public class SubscriptionExpiryJob
{
    private readonly PlayHubDbContext _db;
    private readonly IAlertDispatcher _alerts;

    public SubscriptionExpiryJob(PlayHubDbContext db, IAlertDispatcher alerts)
    {
        _db = db;
        _alerts = alerts;
    }

    public async Task RunAsync()
    {
        var today = DateTime.UtcNow.Date;
        var warnUntil = today.AddDays(7);

        await NotifyExpiringSoonAsync(today, warnUntil);
        await LockExpiredAsync(today);
    }

    private async Task NotifyExpiringSoonAsync(DateTime today, DateTime warnUntil)
    {
        var soon = await _db.Users
            .IgnoreQueryFilters()
            .Where(u =>
                u.IsActive &&
                !u.IsDeleted &&
                u.SubscriptionExpiresAt != null &&
                u.SubscriptionExpiresAt.Value.Date >= today &&
                u.SubscriptionExpiresAt.Value.Date <= warnUntil)
            .ToListAsync();

        foreach (var user in soon)
        {
            var expiry = user.SubscriptionExpiresAt!.Value.Date;
            var daysLeft = Math.Max(0, (expiry - today).Days);

            await _alerts.DispatchToMastersAsync(
                user.TenantId,
                NotificationType.SubscriptionExpiringSoon,
                "Subscription ending soon",
                "الاشتراك هينتهي قريب",
                daysLeft == 0
                    ? $"Subscription for {user.FullName} ends today. Renew to avoid interruption."
                    : $"Subscription for {user.FullName} ends in {daysLeft} day(s) ({expiry:yyyy-MM-dd}).",
                daysLeft == 0
                    ? $"اشتراك {user.FullName} بينتهي النهارده. جدّد عشان ما يتقطعش الشغل."
                    : $"اشتراك {user.FullName} هينتهي بعد {daysLeft} يوم ({expiry:yyyy-MM-dd}).",
                "User",
                user.Id);
        }
    }

    private async Task LockExpiredAsync(DateTime today)
    {
        var expired = await _db.Users
            .IgnoreQueryFilters()
            .Where(u =>
                u.IsActive &&
                !u.IsDeleted &&
                u.SubscriptionExpiresAt != null &&
                u.SubscriptionExpiresAt.Value.Date < today)
            .ToListAsync();

        if (expired.Count == 0) return;

        foreach (var user in expired)
        {
            user.IsActive = false;
            user.SubscriptionLockedAt = DateTime.UtcNow;

            await _alerts.DispatchToMastersAsync(
                user.TenantId,
                NotificationType.SubscriptionExpired,
                "Subscription expired",
                "انتهى الاشتراك",
                $"Subscription for {user.FullName} has expired. Renew to continue.",
                $"انتهى اشتراك {user.FullName}. جدّد الاشتراك عشان تكمل.",
                "User",
                user.Id);
        }

        await _db.SaveChangesAsync();
    }
}
