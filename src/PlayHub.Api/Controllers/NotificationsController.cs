using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Application.Notifications;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService) =>
        _notificationService = notificationService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false, CancellationToken ct = default) =>
        Ok(await _notificationService.GetMyNotificationsAsync(unreadOnly, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct) =>
        Ok(new { count = await _notificationService.GetUnreadCountAsync(ct) });

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            await _notificationService.MarkAsReadAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _notificationService.MarkAllAsReadAsync(ct);
        return NoContent();
    }
}
