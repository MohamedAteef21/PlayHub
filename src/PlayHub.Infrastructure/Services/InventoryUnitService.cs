using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Inventory;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class InventoryUnitService : IInventoryUnitService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public InventoryUnitService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<InventoryUnitDto>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        await EnsureDefaultUnitsAsync(ownerId, ct);

        var q = _db.InventoryUnits.Where(u => u.OwnerUserId == ownerId);
        if (activeOnly)
            q = q.Where(u => u.IsActive);

        return await q
            .OrderBy(u => u.Name)
            .Select(u => new InventoryUnitDto(u.Id, u.Name, u.NameAr, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<InventoryUnitDto> CreateAsync(CreateInventoryUnitRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Unit name is required.");

        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        var exists = await _db.InventoryUnits.AnyAsync(
            u => u.TenantId == _tenantContext.TenantId
                 && u.OwnerUserId == ownerId
                 && u.Name == name, ct);
        if (exists)
            throw new InvalidOperationException("This unit name already exists.");

        var unit = new InventoryUnit
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
            Name = name,
            NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
            IsActive = true
        };
        _db.InventoryUnits.Add(unit);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("InventoryUnit.Created", "InventoryUnit", unit.Id, new { unit.Name }, ct: ct);
        return new InventoryUnitDto(unit.Id, unit.Name, unit.NameAr, unit.IsActive, unit.CreatedAt);
    }

    public async Task<InventoryUnitDto> UpdateAsync(Guid id, UpdateInventoryUnitRequest request, CancellationToken ct = default)
    {
        var unit = await RequireOwnedUnitAsync(id, ct);

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Unit name is required.");

        var ownerId = unit.OwnerUserId ?? await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        var duplicate = await _db.InventoryUnits.AnyAsync(
            u => u.TenantId == _tenantContext.TenantId
                 && u.OwnerUserId == ownerId
                 && u.Name == name
                 && u.Id != id, ct);
        if (duplicate)
            throw new InvalidOperationException("This unit name already exists.");

        var oldName = unit.Name;
        unit.Name = name;
        unit.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
        unit.IsActive = request.IsActive;
        unit.OwnerUserId ??= ownerId;

        // Keep cafeteria item labels in sync — only on this owner's branches.
        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            var ownerBranchIds = await _db.Branches
                .Where(b => b.OwnerUserId == ownerId)
                .Select(b => b.Id)
                .ToListAsync(ct);

            var items = await _db.CafeteriaItems
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == _tenantContext.TenantId
                            && !i.IsDeleted
                            && ownerBranchIds.Contains(i.BranchId)
                            && (i.BaseUnitName == oldName || i.LargeUnitName == oldName))
                .ToListAsync(ct);

            foreach (var item in items)
            {
                if (string.Equals(item.BaseUnitName, oldName, StringComparison.Ordinal))
                    item.BaseUnitName = name;
                if (string.Equals(item.LargeUnitName, oldName, StringComparison.Ordinal))
                    item.LargeUnitName = name;
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("InventoryUnit.Updated", "InventoryUnit", unit.Id, new { oldName, unit.Name, unit.IsActive }, ct: ct);
        return new InventoryUnitDto(unit.Id, unit.Name, unit.NameAr, unit.IsActive, unit.CreatedAt);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var unit = await RequireOwnedUnitAsync(id, ct);

        await EnsureUnitNotUsedOnOpenSessionsAsync(unit.Name, unit.OwnerUserId, ct);

        unit.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        unit.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("InventoryUnit.SoftDeleted", "InventoryUnit", unit.Id, new { unit.Name }, ct: ct);
    }

    /// <summary>
    /// Blocks delete while any open/paused session has cafeteria lines for items that use this unit
    /// on the unit owner's branches.
    /// </summary>
    private async Task EnsureUnitNotUsedOnOpenSessionsAsync(string unitName, Guid? ownerUserId, CancellationToken ct)
    {
        var ownerBranchIds = ownerUserId.HasValue
            ? await _db.Branches.Where(b => b.OwnerUserId == ownerUserId).Select(b => b.Id).ToListAsync(ct)
            : await _db.Branches.Where(b => _tenantContext.AllowedBranchIds.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct);

        var itemIds = await _db.CafeteriaItems
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == _tenantContext.TenantId
                        && !i.IsDeleted
                        && ownerBranchIds.Contains(i.BranchId)
                        && (i.BaseUnitName == unitName || i.LargeUnitName == unitName))
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (itemIds.Count == 0)
            return;

        var hasOpen = await _db.SessionCafeteriaLines
            .AnyAsync(l =>
                itemIds.Contains(l.CafeteriaItemId)
                && l.Session.Status != SessionStatus.Closed
                && !l.Session.IsDeleted, ct);

        if (hasOpen)
        {
            throw new InvalidOperationException(
                "Cannot delete this unit while it is used by cafeteria items on an open session. Close the session first, then delete.");
        }
    }

    public async Task<IReadOnlyList<ItemUnitConversionLogDto>> GetConversionLogsAsync(
        Guid? itemId = null, int take = 50, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        take = Math.Clamp(take, 1, 200);

        var q = _db.ItemUnitConversionLogs
            .Include(l => l.CafeteriaItem)
            .Include(l => l.ChangedByUser)
            .Where(l => l.BranchId == branchId);

        if (itemId.HasValue)
            q = q.Where(l => l.CafeteriaItemId == itemId.Value);

        return await q
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new ItemUnitConversionLogDto(
                l.Id,
                l.CafeteriaItemId,
                l.CafeteriaItem.Name,
                l.OldBaseUnitName,
                l.NewBaseUnitName,
                l.OldLargeUnitName,
                l.NewLargeUnitName,
                l.OldUnitsPerLarge,
                l.NewUnitsPerLarge,
                l.ChangedByUser.FullName,
                l.CreatedAt))
            .ToListAsync(ct);
    }

    private async Task<InventoryUnit> RequireOwnedUnitAsync(Guid id, CancellationToken ct)
    {
        var unit = await _db.InventoryUnits.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("Unit not found.");

        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        if (!OwnerScope.CanAccess(unit.OwnerUserId, ownerId, false))
            throw new KeyNotFoundException("Unit not found.");

        return unit;
    }

    private async Task EnsureDefaultUnitsAsync(Guid ownerId, CancellationToken ct)
    {
        var existingNames = await _db.InventoryUnits
            .Where(u => u.OwnerUserId == ownerId)
            .Select(u => u.Name)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var added = false;

        // Include weight units so recipes can deduct in grams/kg (e.g. sugar bag → tea cup).
        var defaults = new (string Name, string NameAr)[]
        {
            ("قطعة", "قطعة"),
            ("علبة", "علبة"),
            ("كرتونة", "كرتونة"),
            ("جرام", "جرام"),
            ("كجم", "كجم"),
            ("مل", "مل"),
            ("لتر", "لتر"),
        };
        foreach (var (name, nameAr) in defaults)
        {
            if (existingSet.Contains(name))
                continue;
            _db.InventoryUnits.Add(new InventoryUnit
            {
                TenantId = _tenantContext.TenantId,
                OwnerUserId = ownerId,
                Name = name,
                NameAr = nameAr,
                IsActive = true
            });
            existingSet.Add(name);
            added = true;
        }

        // Pull unit names used on this owner's branches into their private catalog.
        var ownerBranchIds = await _db.Branches
            .Where(b => b.OwnerUserId == ownerId)
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (ownerBranchIds.Count > 0)
        {
            var used = await _db.CafeteriaItems
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == _tenantContext.TenantId
                            && !i.IsDeleted
                            && ownerBranchIds.Contains(i.BranchId))
                .Select(i => new { i.BaseUnitName, i.LargeUnitName })
                .ToListAsync(ct);

            var names = used
                .SelectMany(x => new[] { x.BaseUnitName, x.LargeUnitName })
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (existingSet.Contains(name))
                    continue;
                _db.InventoryUnits.Add(new InventoryUnit
                {
                    TenantId = _tenantContext.TenantId,
                    OwnerUserId = ownerId,
                    Name = name,
                    NameAr = name,
                    IsActive = true
                });
                existingSet.Add(name);
                added = true;
            }
        }

        if (added)
            await _db.SaveChangesAsync(ct);
    }
}
