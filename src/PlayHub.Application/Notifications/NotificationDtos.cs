using PlayHub.Domain.Enums;

namespace PlayHub.Application.Notifications;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string? TitleAr,
    string Message,
    string? MessageAr,
    bool IsRead,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAt);

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(bool unreadOnly = false, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
}
