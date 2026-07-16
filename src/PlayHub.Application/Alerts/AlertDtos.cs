using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Alerts;

public record MasterAlertSettingsDto(
    Guid Id,
    Guid UserId,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    bool HasSmtpPassword,
    string? SenderDisplayName,
    string? AlertRecipientEmail,
    string? OwnerWhatsAppPhone,
    bool NotifyLowStock,
    bool NotifySubscription,
    bool NotifyDeviceMaintenance,
    NotificationChannel AllowedChannels);

public record UpsertMasterAlertSettingsRequest(
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    /// <summary>Null/empty keeps existing password.</summary>
    string? SmtpPassword,
    string? SenderDisplayName,
    string? AlertRecipientEmail,
    string? OwnerWhatsAppPhone,
    bool NotifyLowStock,
    bool NotifySubscription,
    bool NotifyDeviceMaintenance);

public record DeviceMaintenanceDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string DeviceIdentifier,
    string RoomName,
    string Reason,
    string? Notes,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string ReportedByName,
    int DaysOpen);

public record StartDeviceMaintenanceRequest(Guid DeviceId, string Reason, string? Notes = null);

public record CompleteDeviceMaintenanceRequest(string? Notes = null);

public interface IAlertSettingsService
{
    Task<MasterAlertSettingsDto> GetMySettingsAsync(CancellationToken ct = default);
    Task<MasterAlertSettingsDto> UpsertMySettingsAsync(UpsertMasterAlertSettingsRequest request, CancellationToken ct = default);
    Task SendTestEmailAsync(CancellationToken ct = default);
}

public interface IDeviceMaintenanceService
{
    Task<IReadOnlyList<DeviceMaintenanceDto>> GetOpenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DeviceMaintenanceDto>> GetHistoryAsync(int take = 50, CancellationToken ct = default);
    Task<DeviceMaintenanceDto> StartAsync(StartDeviceMaintenanceRequest request, CancellationToken ct = default);
    Task<DeviceMaintenanceDto> CompleteAsync(Guid id, CompleteDeviceMaintenanceRequest request, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendAsync(
        MasterAlertSettings settings,
        string toEmail,
        string subject,
        string bodyText,
        byte[]? pdfAttachment = null,
        string? pdfFileName = null,
        CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    Task DispatchToMastersAsync(
        Guid tenantId,
        NotificationType type,
        string titleEn,
        string titleAr,
        string messageEn,
        string messageAr,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken ct = default);
}

public interface IInvoicePdfService
{
    Task<byte[]> BuildSessionInvoicePdfAsync(Guid sessionId, CancellationToken ct = default);
}
