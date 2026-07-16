using Microsoft.AspNetCore.SignalR;
using PlayHub.Api.Hubs;
using PlayHub.Application.Sessions;

namespace PlayHub.Api.Services;

public class SessionHubNotifier : ISessionNotifier
{
    private readonly IHubContext<BranchSessionHub> _hubContext;

    public SessionHubNotifier(IHubContext<BranchSessionHub> hubContext) => _hubContext = hubContext;

    public Task NotifySessionUpdatedAsync(Guid branchId, SessionLiveDto session, CancellationToken ct = default) =>
        _hubContext.Clients.Group(BranchSessionHub.GroupName(branchId))
            .SendAsync("SessionUpdated", session, ct);

    public Task NotifySessionClosedAsync(Guid branchId, Guid sessionId, CancellationToken ct = default) =>
        _hubContext.Clients.Group(BranchSessionHub.GroupName(branchId))
            .SendAsync("SessionClosed", new { sessionId }, ct);
}
