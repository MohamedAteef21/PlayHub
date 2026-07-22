using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AlertSettingsService : IAlertSettingsService
{
    public const string FixedSmtpHost = "smtp.gmail.com";
    public const int FixedSmtpPort = 587;
    public const string FixedSenderDisplayName = "PlayHub System";

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
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.UserId == _tenantContext.UserId, ct);

        return Map(user, settings);
    }

    public async Task<MasterAlertSettingsDto> UpsertMySettingsAsync(
        UpsertMasterAlertSettingsRequest request, CancellationToken ct = default)
    {
        EnsureMaster();
        var user = await LoadUserAsync(ct);

        var settings = await _db.MasterAlertSettings
            .Include(s => s.Recipients)
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

        settings.SmtpHost = FixedSmtpHost;
        settings.SmtpPort = FixedSmtpPort;
        settings.SenderDisplayName = FixedSenderDisplayName;
        settings.SmtpUsername = request.SmtpUsername?.Trim();
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
            settings.SmtpPassword = request.SmtpPassword.Trim();
        settings.OwnerWhatsAppPhone = string.IsNullOrWhiteSpace(request.OwnerWhatsAppPhone)
            ? null
            : PhoneNormalizer.Normalize(request.OwnerWhatsAppPhone);

        ReplaceRecipients(settings, request.Recipients);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AlertSettings.Updated", "MasterAlertSettings", settings.Id, ct: ct);

        return Map(user, settings);
    }

    public async Task SendTestEmailAsync(CancellationToken ct = default)
    {
        EnsureMaster();
        var settings = await _db.MasterAlertSettings
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.UserId == _tenantContext.UserId, ct)
            ?? throw new InvalidOperationException("Save Gmail settings first.");

        var emails = settings.Recipients
            .Select(r => r.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (emails.Count == 0)
            throw new InvalidOperationException("Add at least one alert recipient email.");

        foreach (var email in emails)
        {
            await _email.SendAsync(
                settings,
                email,
                "PlayHub test email / تجربة إيميل",
                "لو وصلت الرسالة دي، إعدادات الجيميل شغالة.\n\nIf you received this, Gmail settings work.",
                ct: ct);
        }
    }

    private void ReplaceRecipients(
        MasterAlertSettings settings,
        IReadOnlyList<UpsertMasterAlertRecipientRequest> recipients)
    {
        if (settings.Recipients.Count > 0)
            _db.MasterAlertRecipients.RemoveRange(settings.Recipients);
        settings.Recipients.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in recipients ?? [])
        {
            var email = r.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Recipient email is required.");
            if (!seen.Add(email))
                throw new InvalidOperationException($"Duplicate recipient email: {email}");

            if (!r.NotifyLowStock && !r.NotifySubscription && !r.NotifyDeviceMaintenance)
                throw new InvalidOperationException(
                    $"Select at least one notification type for {email}.");

            settings.Recipients.Add(new MasterAlertRecipient
            {
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(r.DisplayName) ? null : r.DisplayName.Trim(),
                NotifyLowStock = r.NotifyLowStock,
                NotifySubscription = r.NotifySubscription,
                NotifyDeviceMaintenance = r.NotifyDeviceMaintenance,
            });
        }
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
            s?.SmtpUsername,
            !string.IsNullOrWhiteSpace(s?.SmtpPassword),
            FixedSenderDisplayName,
            s?.OwnerWhatsAppPhone,
            (s?.Recipients ?? [])
                .OrderBy(r => r.Email)
                .Select(r => new MasterAlertRecipientDto(
                    r.Id,
                    r.Email,
                    r.DisplayName,
                    r.NotifyLowStock,
                    r.NotifySubscription,
                    r.NotifyDeviceMaintenance))
                .ToList(),
            user.AllowedNotificationChannels);
}
