using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Notifications;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;

    public NotificationService(PlayHubDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = _db.Notifications.Where(n => n.UserId == _tenantContext.UserId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.TitleAr, n.Message, n.MessageAr,
                n.IsRead, n.RelatedEntityType, n.RelatedEntityId, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.UserId == _tenantContext.UserId && !n.IsRead, ct);

    public async Task MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _tenantContext.UserId, ct)
            ?? throw new KeyNotFoundException("Notification not found.");

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => n.UserId == _tenantContext.UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
