using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public static class BranchGuard
{
    public static Guid RequireBranchId(TenantContext tenantContext)
    {
        if (tenantContext.BranchId is null || tenantContext.BranchId == Guid.Empty)
            throw new InvalidOperationException("A branch must be selected before performing this action. Call POST /api/auth/select-branch first.");

        return tenantContext.BranchId.Value;
    }
}
