using PlayHub.Domain.Enums;

namespace PlayHub.Application.Platform;

public record PlatformAlertSettingsDto(
    Guid? Id,
    string? SmtpUsername,
    bool HasSmtpPassword,
    string? SenderDisplayName,
    bool WhatsAppComingSoon,
    string? WhatsAppIntegrationApiBaseUrl,
    bool HasWhatsAppIntegrationApiKey,
    bool WhatsAppIntegrationEnabled);

public record UpsertPlatformAlertSettingsRequest(
    string? SmtpUsername,
    /// <summary>Null/empty keeps existing password.</summary>
    string? SmtpPassword,
    string? SenderDisplayName,
    string? WhatsAppIntegrationApiBaseUrl,
    /// <summary>Null/empty keeps existing key.</summary>
    string? WhatsAppIntegrationApiKey,
    bool WhatsAppIntegrationEnabled);

public record MasterSubscriptionRowDto(
    Guid Id,
    string Username,
    string FullName,
    DateTime? SubscriptionExpiresAt,
    bool IsActive,
    bool IsLocked,
    int? DaysLeft);

public record SuperAdminDashboardDto(
    int MastersCount,
    int ActiveMastersCount,
    int InactiveMastersCount,
    int StaffCount,
    int TotalUsers,
    int ExpiringWithin7Days,
    int ExpiringWithin30Days,
    int ExpiredOrLocked,
    IReadOnlyList<MasterSubscriptionRowDto> UpcomingExpiries,
    IReadOnlyList<MasterSubscriptionRowDto> LockedOrExpired);

public record NotificationTargetDto(
    Guid UserId,
    string Username,
    string FullName,
    NotificationChannel AllowedChannels,
    bool NotifyLowStock,
    bool NotifySubscription,
    bool NotifyDeviceMaintenance,
    string? AlertRecipientEmail,
    string? OwnerWhatsAppPhone);

public record UpsertNotificationTargetRequest(
    NotificationChannel AllowedChannels,
    bool NotifyLowStock,
    bool NotifySubscription,
    bool NotifyDeviceMaintenance,
    string? AlertRecipientEmail,
    string? OwnerWhatsAppPhone);

public interface IPlatformSettingsService
{
    Task<PlatformAlertSettingsDto> GetAlertSettingsAsync(CancellationToken ct = default);
    Task<PlatformAlertSettingsDto> UpsertAlertSettingsAsync(UpsertPlatformAlertSettingsRequest request, CancellationToken ct = default);
    Task SendPlatformTestEmailAsync(CancellationToken ct = default);
    Task<SuperAdminDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTargetDto>> GetNotificationTargetsAsync(CancellationToken ct = default);
    Task<NotificationTargetDto> UpsertNotificationTargetAsync(Guid userId, UpsertNotificationTargetRequest request, CancellationToken ct = default);
}
