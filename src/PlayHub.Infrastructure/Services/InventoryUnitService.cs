using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Inventory;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
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
        var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        await EnsureDefaultUnitsAsync(ownerId, ct);

        var q = _db.InventoryUnits.AsQueryable();
        if (!_tenantContext.IsSuperAdmin)
            q = q.Where(u => u.OwnerUserId == ownerId);
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

        var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
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

        var ownerId = unit.OwnerUserId ?? await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        var duplicate = await _db.InventoryUnits.AnyAsync(
            u => u.TenantId == _tenantContext.TenantId
                 && u.OwnerUserId == ownerId
                 && u.Name == name
                 && u.Id != id, ct);
        if (duplicate)
            throw new InvalidOperationException("This unit name already exists.");

        unit.Name = name;
        unit.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
        unit.IsActive = request.IsActive;
        unit.OwnerUserId ??= ownerId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("InventoryUnit.Updated", "InventoryUnit", unit.Id, new { unit.Name, unit.IsActive }, ct: ct);
        return new InventoryUnitDto(unit.Id, unit.Name, unit.NameAr, unit.IsActive, unit.CreatedAt);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var unit = await RequireOwnedUnitAsync(id, ct);

        unit.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        unit.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("InventoryUnit.SoftDeleted", "InventoryUnit", unit.Id, new { unit.Name }, ct: ct);
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

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(unit.OwnerUserId, ownerId, false))
                throw new KeyNotFoundException("Unit not found.");
        }

        return unit;
    }

    private async Task EnsureDefaultUnitsAsync(Guid ownerId, CancellationToken ct)
    {
        var hasAny = await _db.InventoryUnits.AnyAsync(
            u => _tenantContext.IsSuperAdmin || u.OwnerUserId == ownerId, ct);
        if (hasAny)
            return;

        var defaults = new[] { "قطعة", "علبة", "كرتونة" };

        foreach (var name in defaults)
        {
            _db.InventoryUnits.Add(new InventoryUnit
            {
                TenantId = _tenantContext.TenantId,
                OwnerUserId = ownerId,
                Name = name,
                NameAr = name,
                IsActive = true
            });
        }

        // Import distinct unit names already used on items in this master's branches.
        var allowedBranches = _tenantContext.AllowedBranchIds;
        var used = await _db.CafeteriaItems
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == _tenantContext.TenantId
                        && !i.IsDeleted
                        && (_tenantContext.IsSuperAdmin || allowedBranches.Contains(i.BranchId)))
            .Select(i => new { i.BaseUnitName, i.LargeUnitName })
            .ToListAsync(ct);

        var names = used
            .SelectMany(x => new[] { x.BaseUnitName, x.LargeUnitName })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in names)
        {
            if (defaults.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            _db.InventoryUnits.Add(new InventoryUnit
            {
                TenantId = _tenantContext.TenantId,
                OwnerUserId = ownerId,
                Name = name,
                NameAr = name,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
