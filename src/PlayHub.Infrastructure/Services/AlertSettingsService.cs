using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AlertSettingsService : IAlertSettingsService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;

    public AlertSettingsService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IEmailSender email,
        IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _audit = audit;
    }

    public async Task<MasterAlertSettingsDto> GetMySettingsAsync(CancellationToken ct = default)
    {
        EnsureMaster();
        var user = await LoadUserAsync(ct);
        var settings = await _db.MasterAlertSettings
            .FirstOrDefaultAsync(s => s.UserId == _tenantContext.UserId, ct);

        return Map(user, settings);
    }

    public async Task<MasterAlertSettingsDto> UpsertMySettingsAsync(
        UpsertMasterAlertSettingsRequest request, CancellationToken ct = default)
    {
        EnsureMaster();
        var user = await LoadUserAsync(ct);

        var settings = await _db.MasterAlertSettings
            .FirstOrDefaultAsync(s => s.UserId == _tenantContext.UserId, ct);

        if (settings is null)
        {
            settings = new MasterAlertSettings
            {
                TenantId = _tenantContext.TenantId,
                UserId = _tenantContext.UserId
            };
            _db.MasterAlertSettings.Add(settings);
        }

        settings.SmtpHost = string.IsNullOrWhiteSpace(request.SmtpHost) ? "smtp.gmail.com" : request.SmtpHost.Trim();
        settings.SmtpPort = request.SmtpPort > 0 ? request.SmtpPort : 587;
        settings.SmtpUsername = request.SmtpUsername?.Trim();
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
            settings.SmtpPassword = request.SmtpPassword.Trim();
        settings.SenderDisplayName = request.SenderDisplayName?.Trim();
        settings.AlertRecipientEmail = request.AlertRecipientEmail?.Trim();
        settings.OwnerWhatsAppPhone = string.IsNullOrWhiteSpace(request.OwnerWhatsAppPhone)
            ? null
            : PhoneNormalizer.Normalize(request.OwnerWhatsAppPhone);
        settings.NotifyLowStock = request.NotifyLowStock;
        settings.NotifySubscription = request.NotifySubscription;
        settings.NotifyDeviceMaintenance = request.NotifyDeviceMaintenance;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AlertSettings.Updated", "MasterAlertSettings", settings.Id, ct: ct);

        return Map(user, settings)!;
    }

    public async Task SendTestEmailAsync(CancellationToken ct = default)
    {
        EnsureMaster();
        var settings = await _db.MasterAlertSettings
            .FirstOrDefaultAsync(s => s.UserId == _tenantContext.UserId, ct)
            ?? throw new InvalidOperationException("Save Gmail settings first.");

        if (string.IsNullOrWhiteSpace(settings.AlertRecipientEmail))
            throw new InvalidOperationException("Alert recipient email is required.");

        await _email.SendAsync(
            settings,
            settings.AlertRecipientEmail,
            "PlayHub test email / تجربة إيميل",
            "لو وصلت الرسالة دي، إعدادات الجيميل شغالة.\n\nIf you received this, Gmail settings work.",
            ct: ct);
    }

    private async Task<User> LoadUserAsync(CancellationToken ct) =>
        await _db.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.Id == _tenantContext.UserId && !u.IsDeleted, ct);

    private void EnsureMaster()
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Only Master Admin can manage alert settings.");
    }

    private static MasterAlertSettingsDto Map(User user, MasterAlertSettings? s) =>
        new(
            s?.Id ?? Guid.Empty,
            user.Id,
            s?.SmtpHost,
            s?.SmtpPort ?? 587,
            s?.SmtpUsername,
            !string.IsNullOrWhiteSpace(s?.SmtpPassword),
            s?.SenderDisplayName,
            s?.AlertRecipientEmail,
            s?.OwnerWhatsAppPhone,
            s?.NotifyLowStock ?? true,
            s?.NotifySubscription ?? true,
            s?.NotifyDeviceMaintenance ?? true,
            user.AllowedNotificationChannels);
}
