using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Api.Hubs;

[Authorize]
public class BranchSessionHub : Hub
{
    public static string GroupName(Guid branchId) => $"branch-{branchId}";

    public override async Task OnConnectedAsync()
    {
        var branchId = GetBranchIdFromContext();
        if (branchId.HasValue && CanAccessBranch(branchId.Value))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId.Value));

        await base.OnConnectedAsync();
    }

    /// <summary>Join the SignalR group for a branch to receive live session updates.</summary>
    public async Task JoinBranch(Guid branchId)
    {
        if (!CanAccessBranch(branchId))
            throw new HubException("You are not authorized for this branch.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));
    }

    public async Task LeaveBranch(Guid branchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(branchId));
    }

    private Guid? GetBranchIdFromContext()
    {
        var claim = Context.User?.FindFirst("branch_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool CanAccessBranch(Guid branchId)
    {
        var tenant = Context.GetHttpContext()?.RequestServices.GetService(typeof(TenantContext)) as TenantContext;
        if (tenant is null)
            return false;

        if (tenant.IsSuperAdmin)
            return true;

        return tenant.AllowedBranchIds.Contains(branchId);
    }
}
