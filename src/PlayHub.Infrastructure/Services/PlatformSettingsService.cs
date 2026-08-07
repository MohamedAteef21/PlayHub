using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Application.Platform;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class PlatformSettingsService : IPlatformSettingsService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;

    public PlatformSettingsService(
        PlayHubDbContext db,
        TenantContext tenant,
        IEmailSender email,
        IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _email = email;
        _audit = audit;
    }

    public async Task<PlatformAlertSettingsDto> GetAlertSettingsAsync(CancellationToken ct = default)
    {
        EnsureSuperAdmin();
        var settings = await _db.PlatformAlertSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, ct);
        return Map(settings);
    }

    public async Task<PlatformAlertSettingsDto> UpsertAlertSettingsAsync(
        UpsertPlatformAlertSettingsRequest request,
        CancellationToken ct = default)
    {
        EnsureSuperAdmin();

        var settings = await _db.PlatformAlertSettings
            .FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, ct);

        if (settings is null)
        {
            settings = new PlatformAlertSettings { TenantId = _tenant.TenantId };
            _db.PlatformAlertSettings.Add(settings);
        }

        settings.SmtpUsername = string.IsNullOrWhiteSpace(request.SmtpUsername)
            ? null
            : request.SmtpUsername.Trim();
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
            settings.SmtpPassword = request.SmtpPassword.Trim();
        settings.SenderDisplayName = string.IsNullOrWhiteSpace(request.SenderDisplayName)
            ? "PlayHub System"
            : request.SenderDisplayName.Trim();

        settings.WhatsAppIntegrationApiBaseUrl = string.IsNullOrWhiteSpace(request.WhatsAppIntegrationApiBaseUrl)
            ? null
            : request.WhatsAppIntegrationApiBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.WhatsAppIntegrationApiKey))
            settings.WhatsAppIntegrationApiKey = request.WhatsAppIntegrationApiKey.Trim();
        settings.WhatsAppIntegrationEnabled = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PlatformAlertSettings.Updated", "PlatformAlertSettings", settings.Id, ct: ct);
        return Map(settings);
    }

    public async Task SendPlatformTestEmailAsync(CancellationToken ct = default)
    {
        EnsureSuperAdmin();
        var settings = await _db.PlatformAlertSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, ct)
            ?? throw new InvalidOperationException("Configure the platform Gmail first.");

        if (string.IsNullOrWhiteSpace(settings.SmtpUsername) || string.IsNullOrWhiteSpace(settings.SmtpPassword))
            throw new InvalidOperationException("Gmail address and App Password are required.");

        var to = settings.SmtpUsername.Trim();
        await _email.SendWithCredentialsAsync(
            settings.SmtpUsername.Trim(),
            settings.SmtpPassword,
            settings.SenderDisplayName,
            to,
            "PlayHub — Test email",
            "Platform Gmail is configured correctly.\nتم ضبط جيميل المنصة بنجاح.",
            ct: ct);
    }

    public async Task<SuperAdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureSuperAdmin();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == _tenant.TenantId && !u.IsDeleted)
            .Select(u => new UserDashRow(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.IsMaster,
                u.IsActive,
                u.SubscriptionExpiresAt,
                u.SubscriptionLockedAt))
            .ToListAsync(ct);

        var masters = users
            .Where(u => u.Role == UserRole.MasterAdmin || (u.IsMaster && u.Role != UserRole.SuperAdmin))
            .ToList();
        var staff = users
            .Where(u => !u.IsMaster && u.Role != UserRole.SuperAdmin)
            .ToList();
        var today = DateTime.UtcNow.Date;

        var masterRows = masters.Select(u => ToSubscriptionRow(u, today)).ToList();

        var upcoming = masterRows
            .Where(m => m.DaysLeft is >= 0 and <= 30)
            .OrderBy(m => m.SubscriptionExpiresAt)
            .Take(20)
            .ToList();

        var lockedOrExpired = masterRows
            .Where(m => m.IsLocked || m.DaysLeft is < 0)
            .OrderBy(m => m.SubscriptionExpiresAt)
            .Take(20)
            .ToList();

        return new SuperAdminDashboardDto(
            masters.Count,
            masters.Count(m => m.IsActive && m.SubscriptionLockedAt is null),
            masters.Count(m => !m.IsActive || m.SubscriptionLockedAt is not null),
            staff.Count,
            users.Count,
            masterRows.Count(m => m.DaysLeft is >= 0 and <= 7),
            masterRows.Count(m => m.DaysLeft is >= 0 and <= 30),
            lockedOrExpired.Count,
            upcoming,
            lockedOrExpired);
    }

    public async Task<IReadOnlyList<NotificationTargetDto>> GetNotificationTargetsAsync(CancellationToken ct = default)
    {
        EnsureSuperAdmin();

        var masters = await _db.Users
            .AsNoTracking()
            .Include(u => u.AlertSettings)
            .Where(u =>
                u.TenantId == _tenant.TenantId &&
                !u.IsDeleted &&
                (u.Role == UserRole.MasterAdmin || (u.IsMaster && u.Role != UserRole.SuperAdmin)))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.Email)
            .ToListAsync(ct);

        return masters.Select(MapTarget).ToList();
    }

    public async Task<NotificationTargetDto> UpsertNotificationTargetAsync(
        Guid userId,
        UpsertNotificationTargetRequest request,
        CancellationToken ct = default)
    {
        EnsureSuperAdmin();

        var user = await _db.Users
            .Include(u => u.AlertSettings)
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.TenantId == _tenant.TenantId &&
                !u.IsDeleted &&
                (u.Role == UserRole.MasterAdmin || (u.IsMaster && u.Role != UserRole.SuperAdmin)), ct)
            ?? throw new KeyNotFoundException("Owner not found.");

        user.AllowedNotificationChannels = request.AllowedChannels;

        var settings = user.AlertSettings;
        if (settings is null)
        {
            settings = new MasterAlertSettings
            {
                TenantId = _tenant.TenantId,
                UserId = user.Id
            };
            _db.MasterAlertSettings.Add(settings);
            user.AlertSettings = settings;
        }

        settings.NotifyLowStock = request.NotifyLowStock;
        settings.NotifySubscription = request.NotifySubscription;
        settings.NotifyDeviceMaintenance = request.NotifyDeviceMaintenance;
        settings.AlertRecipientEmail = string.IsNullOrWhiteSpace(request.AlertRecipientEmail)
            ? null
            : request.AlertRecipientEmail.Trim();
        settings.OwnerWhatsAppPhone = string.IsNullOrWhiteSpace(request.OwnerWhatsAppPhone)
            ? null
            : request.OwnerWhatsAppPhone.Trim();

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("NotificationTarget.Updated", "User", user.Id, ct: ct);
        return MapTarget(user);
    }

    private static NotificationTargetDto MapTarget(User u)
    {
        var s = u.AlertSettings;
        return new NotificationTargetDto(
            u.Id,
            u.Email,
            u.FullName,
            u.AllowedNotificationChannels,
            s?.NotifyLowStock ?? true,
            s?.NotifySubscription ?? true,
            s?.NotifyDeviceMaintenance ?? true,
            s?.AlertRecipientEmail,
            s?.OwnerWhatsAppPhone);
    }

    private static MasterSubscriptionRowDto ToSubscriptionRow(UserDashRow u, DateTime today)
    {
        int? daysLeft = u.SubscriptionExpiresAt is null
            ? null
            : (int)Math.Ceiling((u.SubscriptionExpiresAt.Value.Date - today).TotalDays);

        return new MasterSubscriptionRowDto(
            u.Id,
            u.Email,
            $"{u.FirstName} {u.LastName}".Trim(),
            u.SubscriptionExpiresAt,
            u.IsActive,
            u.SubscriptionLockedAt.HasValue,
            daysLeft);
    }

    private void EnsureSuperAdmin()
    {
        if (!_tenant.IsSuperAdmin)
            throw new UnauthorizedAccessException("Super Admin only.");
    }

    private static PlatformAlertSettingsDto Map(PlatformAlertSettings? s) =>
        new(
            s?.Id,
            s?.SmtpUsername,
            !string.IsNullOrWhiteSpace(s?.SmtpPassword),
            s?.SenderDisplayName ?? "PlayHub System",
            true,
            s?.WhatsAppIntegrationApiBaseUrl,
            !string.IsNullOrWhiteSpace(s?.WhatsAppIntegrationApiKey),
            s?.WhatsAppIntegrationEnabled ?? false);

    private sealed record UserDashRow(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        UserRole Role,
        bool IsMaster,
        bool IsActive,
        DateTime? SubscriptionExpiresAt,
        DateTime? SubscriptionLockedAt);
}
