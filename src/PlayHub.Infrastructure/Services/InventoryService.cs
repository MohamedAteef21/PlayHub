using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Cafeteria;
using PlayHub.Application.Common;
using PlayHub.Application.Inventory;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;
    private readonly LowStockNotifier _lowStock;

    public InventoryService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAuditService audit,
        LowStockNotifier lowStock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _lowStock = lowStock;
    }

    public async Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(
        Guid? itemId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.InventoryMovements
            .Include(m => m.CafeteriaItem)
            .Include(m => m.PerformedByUser)
            .Where(m => m.BranchId == branchId);

        if (itemId.HasValue)
            query = query.Where(m => m.CafeteriaItemId == itemId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(size)
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.CafeteriaItemId,
                m.CafeteriaItem.Name,
                m.MovementType,
                m.QuantityChange,
                m.ReferenceType,
                m.ReferenceId,
                m.Notes,
                m.PerformedByUser.FullName,
                m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<InventoryMovementDto>(items, total, p, size);
    }

    public async Task<CafeteriaItemDto> AdjustQuantityAsync(Guid itemId, AdjustInventoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Adjustment reason is required.");

        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == itemId && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        if (request.NewQuantity < 0)
            throw new InvalidOperationException("Quantity cannot be negative.");

        var change = request.NewQuantity - item.CurrentQuantity;
        if (change == 0)
            return MapItem(item);

        var voucher = await CreateVoucherAsync(new CreateStockVoucherRequest(
            StockVoucherType.Settlement,
            [new StockVoucherLineInput(itemId, change)],
            request.Reason.Trim()), ct);

        await PostVoucherAsync(voucher.Id, ct);

        await _db.Entry(item).ReloadAsync(ct);
        return MapItem(item);
    }

    public async Task<PagedResult<StockVoucherDto>> GetVouchersAsync(
        StockVoucherType? type = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.StockVouchers
            .Include(v => v.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(v => v.CreatedByUser)
            .Include(v => v.PostedByUser)
            .Where(v => v.BranchId == branchId);

        if (type.HasValue)
            query = query.Where(v => v.VoucherType == type.Value);

        var total = await query.CountAsync(ct);
        var list = await query.OrderByDescending(v => v.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<StockVoucherDto>(list.Select(MapVoucher).ToList(), total, p, size);
    }

    public async Task<StockVoucherDto?> GetVoucherAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var voucher = await _db.StockVouchers
            .Include(v => v.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(v => v.CreatedByUser)
            .Include(v => v.PostedByUser)
            .FirstOrDefaultAsync(v => v.Id == id && v.BranchId == branchId, ct);
        return voucher is null ? null : MapVoucher(voucher);
    }

    public async Task<StockVoucherDto> CreateVoucherAsync(CreateStockVoucherRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one voucher line is required.");

        if (request.VoucherType == StockVoucherType.StockIn && request.Lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Stock-in quantities must be positive.");

        if (request.VoucherType == StockVoucherType.StockCount && request.Lines.Any(l => l.Quantity < 0))
            throw new InvalidOperationException("Counted quantities cannot be negative.");

        if (request.RelatedCountVoucherId.HasValue && request.VoucherType != StockVoucherType.Settlement)
            throw new InvalidOperationException("Related count voucher is only valid for settlements.");

        StockVoucher? relatedCount = null;
        if (request.RelatedCountVoucherId.HasValue)
        {
            relatedCount = await _db.StockVouchers
                .Include(v => v.Lines)
                .FirstOrDefaultAsync(v =>
                    v.Id == request.RelatedCountVoucherId &&
                    v.BranchId == branchId &&
                    v.VoucherType == StockVoucherType.StockCount, ct)
                ?? throw new KeyNotFoundException("Related count voucher not found.");

            if (relatedCount.Status != StockVoucherStatus.Posted)
                throw new InvalidOperationException("Related count voucher must be posted first.");
        }

        var branch = await _db.Branches.FirstAsync(b => b.Id == branchId, ct);
        var prefix = request.VoucherType switch
        {
            StockVoucherType.StockIn => "SI",
            StockVoucherType.StockCount => "SC",
            StockVoucherType.Settlement => "ST",
            _ => "SV"
        };
        var number = $"{prefix}-{branch.NextStockVoucherNumber:D5}";
        branch.NextStockVoucherNumber++;

        var voucher = new StockVoucher
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            VoucherNumber = number,
            VoucherType = request.VoucherType,
            Status = StockVoucherStatus.Draft,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RelatedCountVoucherId = relatedCount?.Id,
            CreatedByUserId = _tenantContext.UserId
        };

        foreach (var line in request.Lines)
        {
            var item = await _db.CafeteriaItems.FirstOrDefaultAsync(
                i => i.Id == line.CafeteriaItemId && i.BranchId == branchId, ct)
                ?? throw new KeyNotFoundException($"Cafeteria item {line.CafeteriaItemId} not found.");

            int? systemQty = request.VoucherType is StockVoucherType.StockCount or StockVoucherType.Settlement
                ? item.CurrentQuantity
                : null;

            // Always persist Quantity in base (small) units.
            int baseQty;
            var enteredUnit = line.Unit;
            var notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim();

            if (request.VoucherType == StockVoucherType.StockIn)
            {
                baseQty = ItemUnitHelper.ToBaseQuantity(item, line.Quantity, enteredUnit);
                if (enteredUnit == InventoryUnitKind.Large)
                {
                    var convertNote = $"Entered {line.Quantity} {item.LargeUnitName} (= {baseQty} {item.BaseUnitName})";
                    notes = notes is null ? convertNote : $"{notes} · {convertNote}";
                }
            }
            else
            {
                // Count & settlement are always in base units.
                if (enteredUnit != InventoryUnitKind.Base)
                    throw new InvalidOperationException("Count and settlement quantities must be in the small (base) unit.");
                if (line.Quantity < 0 && request.VoucherType == StockVoucherType.StockCount)
                    throw new InvalidOperationException("Counted quantities cannot be negative.");
                baseQty = line.Quantity;
                enteredUnit = InventoryUnitKind.Base;
            }

            voucher.Lines.Add(new StockVoucherLine
            {
                CafeteriaItemId = item.Id,
                Quantity = baseQty,
                EnteredQuantity = line.Quantity,
                EnteredUnit = enteredUnit,
                SystemQuantity = systemQty,
                Notes = notes
            });
        }

        _db.StockVouchers.Add(voucher);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("StockVoucher.Created", "StockVoucher", voucher.Id, new
        {
            voucher.VoucherNumber,
            voucher.VoucherType,
            LineCount = voucher.Lines.Count
        }, ct: ct);

        await _db.Entry(voucher).Reference(v => v.CreatedByUser).LoadAsync(ct);
        await _db.Entry(voucher).Collection(v => v.Lines).Query().Include(l => l.CafeteriaItem).LoadAsync(ct);
        return MapVoucher(voucher);
    }

    public async Task<StockVoucherDto> CreateSettlementFromCountAsync(Guid countVoucherId, string? notes = null, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var count = await _db.StockVouchers
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v =>
                v.Id == countVoucherId &&
                v.BranchId == branchId &&
                v.VoucherType == StockVoucherType.StockCount, ct)
            ?? throw new KeyNotFoundException("Count voucher not found.");

        if (count.Status != StockVoucherStatus.Posted)
            throw new InvalidOperationException("Count voucher must be posted before creating a settlement.");

        var lines = count.Lines
            .Where(l => l.SystemQuantity.HasValue && l.Quantity != l.SystemQuantity.Value)
            .Select(l => new StockVoucherLineInput(l.CafeteriaItemId, l.Quantity - l.SystemQuantity!.Value))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException("Count has no variances to settle.");

        return await CreateVoucherAsync(new CreateStockVoucherRequest(
            StockVoucherType.Settlement,
            lines,
            notes ?? $"Settlement from {count.VoucherNumber}",
            count.Id), ct);
    }

    public async Task<StockVoucherDto> PostVoucherAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var voucher = await _db.StockVouchers
            .Include(v => v.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(v => v.CreatedByUser)
            .FirstOrDefaultAsync(v => v.Id == id && v.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Stock voucher not found.");

        if (voucher.Status != StockVoucherStatus.Draft)
            throw new InvalidOperationException("Only draft vouchers can be posted.");

        foreach (var line in voucher.Lines)
        {
            var item = line.CafeteriaItem;
            int change;
            InventoryMovementType movementType;
            string? notes = voucher.Notes;

            switch (voucher.VoucherType)
            {
                case StockVoucherType.StockIn:
                    change = line.Quantity;
                    item.CurrentQuantity += change;
                    movementType = InventoryMovementType.StockIn;
                    break;

                case StockVoucherType.StockCount:
                    // Snapshot only — no stock change. Refresh system qty at post time.
                    line.SystemQuantity = item.CurrentQuantity;
                    change = 0;
                    movementType = InventoryMovementType.StockCount;
                    notes = $"Counted {line.Quantity} (system {line.SystemQuantity})";
                    break;

                case StockVoucherType.Settlement:
                    // Quantity is signed delta. For lines from absolute adjust UI we already stored delta.
                    change = line.Quantity;
                    if (item.CurrentQuantity + change < 0)
                        throw new InvalidOperationException($"Settlement would make {item.Name} negative.");
                    line.SystemQuantity ??= item.CurrentQuantity;
                    item.CurrentQuantity += change;
                    movementType = InventoryMovementType.Settlement;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported voucher type.");
            }

            if (voucher.VoucherType == StockVoucherType.StockCount || change != 0)
            {
                _db.InventoryMovements.Add(new InventoryMovement
                {
                    TenantId = _tenantContext.TenantId,
                    BranchId = branchId,
                    CafeteriaItemId = item.Id,
                    MovementType = movementType,
                    QuantityChange = change,
                    ReferenceType = "StockVoucher",
                    ReferenceId = voucher.Id,
                    Notes = notes,
                    PerformedByUserId = _tenantContext.UserId
                });
            }

            if (change != 0)
                await _lowStock.CheckAndNotifyAsync(item, ct);
        }

        voucher.Status = StockVoucherStatus.Posted;
        voucher.PostedAt = DateTime.UtcNow;
        voucher.PostedByUserId = _tenantContext.UserId;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("StockVoucher.Posted", "StockVoucher", voucher.Id, new
        {
            voucher.VoucherNumber,
            voucher.VoucherType
        }, ct: ct);

        await _db.Entry(voucher).Reference(v => v.PostedByUser).LoadAsync(ct);
        return MapVoucher(voucher);
    }

    private static StockVoucherDto MapVoucher(StockVoucher v) =>
        new(
            v.Id,
            v.BranchId,
            v.VoucherNumber,
            v.VoucherType,
            v.Status,
            v.Notes,
            v.RelatedCountVoucherId,
            v.CreatedByUser.FullName,
            v.CreatedAt,
            v.PostedAt,
            v.PostedByUser?.FullName,
            v.Lines.Select(l => new StockVoucherLineDto(
                l.Id,
                l.CafeteriaItemId,
                l.CafeteriaItem.Name,
                l.Quantity,
                l.SystemQuantity,
                l.SystemQuantity.HasValue && v.VoucherType == StockVoucherType.StockCount
                    ? l.Quantity - l.SystemQuantity.Value
                    : v.VoucherType == StockVoucherType.Settlement
                        ? l.Quantity
                        : null,
                l.EnteredQuantity,
                l.EnteredUnit,
                l.Notes)).ToList());

    private static CafeteriaItemDto MapItem(CafeteriaItem i) =>
        new(i.Id, i.BranchId, i.Name, i.NameAr, i.SellPrice, i.CurrentQuantity, i.MinThreshold,
            i.CurrentQuantity <= i.MinThreshold, i.IsActive, i.Kind,
            i.BaseUnitName, i.LargeUnitName, i.UnitsPerLarge, i.CreatedAt,
            (i.Variants ?? [])
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Name)
                .Select(v => new CafeteriaItemVariantDto(
                    v.Id, v.Name, v.SellPrice, v.IsActive, v.SortOrder,
                    (v.RecipeLines ?? [])
                        .Select(r => new RecipeLineDto(
                            r.Id, r.WarehouseItemId, r.WarehouseItem?.Name ?? "", r.Quantity,
                            r.WarehouseItem?.CurrentQuantity ?? 0))
                        .ToList()))
                .ToList());
}
