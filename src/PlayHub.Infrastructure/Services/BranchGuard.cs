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
        await EnsureOwnedBranchAsync(db, tenantContext, branchId, ct);
        return branchId;
    }

    /// <summary>
    /// Resolve the branch for a create/write. Masters may pass an explicit <paramref name="requestedBranchId"/>
    /// (required when no active branch / viewing all). Staff always use the selected active branch.
    /// </summary>
    public static async Task<Guid> ResolveCreateBranchIdAsync(
        PlayHubDbContext db,
        TenantContext tenantContext,
        Guid? requestedBranchId,
        CancellationToken ct = default)
    {
        if (!tenantContext.IsMaster)
        {
            // Employees cannot retarget creates — always the branch they opened the app on.
            return await RequireOwnedBranchIdAsync(db, tenantContext, ct);
        }

        if (requestedBranchId is Guid requested && requested != Guid.Empty)
        {
            if (!tenantContext.IsSuperAdmin
                && tenantContext.AllowedBranchIds.Count > 0
                && !tenantContext.AllowedBranchIds.Contains(requested))
            {
                throw new UnauthorizedAccessException("You do not have access to this branch.");
            }

            await EnsureOwnedBranchAsync(db, tenantContext, requested, ct);
            return requested;
        }

        if (tenantContext.BranchId is Guid active && active != Guid.Empty)
            return await RequireOwnedBranchIdAsync(db, tenantContext, ct);

        throw new InvalidOperationException(
            "A branch must be selected when creating this item. Pass branchId in the request body.");
    }

    /// <summary>
    /// For master read/list views: null means all allowed branches (EF filters). Staff always get their active branch.
    /// </summary>
    public static Guid? ResolveReadBranchId(TenantContext tenantContext)
    {
        if (!tenantContext.IsMaster)
            return RequireBranchId(tenantContext);

        if (tenantContext.BranchId is Guid id && id != Guid.Empty)
        {
            if (!tenantContext.IsSuperAdmin
                && tenantContext.AllowedBranchIds.Count > 0
                && !tenantContext.AllowedBranchIds.Contains(id))
            {
                throw new UnauthorizedAccessException("You do not have access to this branch.");
            }

            return id;
        }

        return null; // all allowed branches
    }

    private static async Task EnsureOwnedBranchAsync(
        PlayHubDbContext db,
        TenantContext tenantContext,
        Guid branchId,
        CancellationToken ct)
    {
        if (tenantContext.IsSuperAdmin)
            return;

        var businessOwnerId = await OwnerScope.ResolveBusinessOwnerIdAsync(db, tenantContext, ct);
        var branchOwnerId = await db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.OwnerUserId)
            .FirstOrDefaultAsync(ct);

        if (branchOwnerId.HasValue && branchOwnerId.Value != businessOwnerId)
            throw new UnauthorizedAccessException("You do not have access to this branch.");
    }
}
