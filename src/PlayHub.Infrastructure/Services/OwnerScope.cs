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

    /// <summary>
    /// Catalog / asset isolation owner.
    /// Master/Staff → business owner. SuperAdmin on a branch → that branch's owner (so they don't see other masters' catalogs).
    /// SuperAdmin with no branch → null (no owner filter).
    /// </summary>
    public static async Task<Guid?> ResolveCatalogOwnerFilterAsync(
        PlayHubDbContext db,
        TenantContext tenant,
        CancellationToken ct = default)
    {
        if (tenant.IsSuperAdmin)
        {
            if (tenant.BranchId is not Guid branchId)
                return null;

            return await db.Branches.AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => b.OwnerUserId)
                .FirstOrDefaultAsync(ct);
        }

        return await ResolveBusinessOwnerIdAsync(db, tenant, ct);
    }

    /// <summary>
    /// Owner id for creating catalog rows. Prefer branch owner when SuperAdmin is on a branch.
    /// </summary>
    public static async Task<Guid> ResolveCatalogOwnerIdAsync(
        PlayHubDbContext db,
        TenantContext tenant,
        CancellationToken ct = default)
    {
        var filter = await ResolveCatalogOwnerFilterAsync(db, tenant, ct);
        return filter ?? await ResolveBusinessOwnerIdAsync(db, tenant, ct);
    }

    public static bool CanAccess(Guid? ownerUserId, Guid businessOwnerId, bool isSuperAdmin) =>
        isSuperAdmin || (ownerUserId.HasValue && ownerUserId.Value == businessOwnerId);
}
