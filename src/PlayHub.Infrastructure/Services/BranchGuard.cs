using Microsoft.EntityFrameworkCore;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public static class BranchGuard
{
    public static Guid RequireBranchId(TenantContext tenantContext)
    {
        if (tenantContext.BranchId is null || tenantContext.BranchId == Guid.Empty)
            throw new InvalidOperationException("A branch must be selected before performing this action. Call POST /api/auth/select-branch first.");

        var branchId = tenantContext.BranchId.Value;

        // Defense in depth: non-SuperAdmin may only use an allowed branch.
        if (!tenantContext.IsSuperAdmin
            && tenantContext.AllowedBranchIds.Count > 0
            && !tenantContext.AllowedBranchIds.Contains(branchId))
        {
            throw new UnauthorizedAccessException("You do not have access to this branch.");
        }

        return branchId;
    }

    /// <summary>
    /// Selected branch must belong to the business owner (blocks cross-master access via stray UserBranch).
    /// </summary>
    public static async Task<Guid> RequireOwnedBranchIdAsync(
        PlayHubDbContext db,
        TenantContext tenantContext,
        CancellationToken ct = default)
    {
        var branchId = RequireBranchId(tenantContext);
        if (tenantContext.IsSuperAdmin)
            return branchId;

        var businessOwnerId = await OwnerScope.ResolveBusinessOwnerIdAsync(db, tenantContext, ct);
        var branchOwnerId = await db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.OwnerUserId)
            .FirstOrDefaultAsync(ct);

        if (branchOwnerId.HasValue && branchOwnerId.Value != businessOwnerId)
            throw new UnauthorizedAccessException("You do not have access to this branch.");

        return branchId;
    }
}
