using Microsoft.EntityFrameworkCore;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

/// <summary>
/// Resolves the business owner for master-scoped catalogs.
/// All Master Admins share one TenantId; catalogs are isolated by OwnerUserId.
/// </summary>
public static class OwnerScope
{
    /// <summary>
    /// Master/SuperAdmin → self. Staff → ParentUserId (their master).
    /// </summary>
    public static async Task<Guid> ResolveBusinessOwnerIdAsync(
        PlayHubDbContext db,
        TenantContext tenant,
        CancellationToken ct = default)
    {
        if (tenant.UserId == Guid.Empty)
            throw new InvalidOperationException("Authenticated user is required.");

        if (tenant.Role is UserRole.SuperAdmin or UserRole.MasterAdmin)
            return tenant.UserId;

        var parentId = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == tenant.UserId && !u.IsDeleted)
            .Select(u => u.ParentUserId)
            .FirstOrDefaultAsync(ct);

        return parentId ?? tenant.UserId;
    }

    public static bool CanAccess(Guid? ownerUserId, Guid businessOwnerId, bool isSuperAdmin) =>
        isSuperAdmin || (ownerUserId.HasValue && ownerUserId.Value == businessOwnerId);
}
