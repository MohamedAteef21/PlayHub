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
}
