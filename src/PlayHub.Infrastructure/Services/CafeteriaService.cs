using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Cafeteria;
using PlayHub.Application.Common;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class CafeteriaService : ICafeteriaService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;
    private readonly BillingService _billing;
    private readonly LowStockNotifier _lowStock;

    public CafeteriaService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAuditService audit,
        BillingService billing,
        LowStockNotifier lowStock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _billing = billing;
        _lowStock = lowStock;
    }

    public async Task<IReadOnlyList<CafeteriaItemDto>> GetItemsAsync(CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);
        return await _db.CafeteriaItems
            .Where(i => i.BranchId == branchId)
            .OrderBy(i => i.Name)
            .Select(i => MapItem(i))
            .ToListAsync(ct);
    }

    public async Task<CafeteriaItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);
        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct);
        return item is null ? null : MapItem(item);
    }

    public async Task<CafeteriaItemDto> CreateItemAsync(CreateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        var (baseUnit, largeUnit, unitsPerLarge) = await ResolveUnitsAsync(
            request.BaseUnitId, request.LargeUnitId, request.UnitsPerLarge, ct);

        var tempItem = new CafeteriaItem
        {
            Name = request.Name.Trim(),
            LargeUnitName = largeUnit,
            UnitsPerLarge = unitsPerLarge,
            SellPrice = request.SellPrice
        };
        var stockInBase = request.CurrentQuantity <= 0
            ? 0
            : ItemUnitHelper.ToBaseQuantity(tempItem, request.CurrentQuantity, request.InitialStockUnit);

        var item = new CafeteriaItem
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            NameAr = request.NameAr?.Trim(),
            SellPrice = request.SellPrice,
            CurrentQuantity = stockInBase,
            MinThreshold = request.MinThreshold,
            BaseUnitName = baseUnit,
            LargeUnitName = largeUnit,
            UnitsPerLarge = unitsPerLarge
        };

        _db.CafeteriaItems.Add(item);

        if (stockInBase > 0)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = item.Id,
                MovementType = InventoryMovementType.InitialStock,
                QuantityChange = stockInBase,
                ReferenceType = "CafeteriaItem",
                ReferenceId = item.Id,
                Notes = request.InitialStockUnit == InventoryUnitKind.Large && largeUnit is not null
                    ? $"Initial stock: {request.CurrentQuantity} {largeUnit} (= {stockInBase} {baseUnit})"
                    : "Initial stock",
                PerformedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Created", "CafeteriaItem", item.Id, new { item.Name, item.BaseUnitName, item.LargeUnitName, item.UnitsPerLarge }, ct: ct);
        await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    public async Task<CafeteriaItemDto> UpdateItemAsync(Guid id, UpdateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        var oldBase = item.BaseUnitName;
        var oldLarge = item.LargeUnitName;
        var oldFactor = item.UnitsPerLarge;

        var (baseUnit, largeUnit, unitsPerLarge) = await ResolveUnitsAsync(
            request.BaseUnitId, request.LargeUnitId, request.UnitsPerLarge, ct);

        item.Name = request.Name.Trim();
        item.NameAr = request.NameAr?.Trim();
        item.SellPrice = request.SellPrice;
        item.MinThreshold = request.MinThreshold;
        item.IsActive = request.IsActive;
        item.BaseUnitName = baseUnit;
        item.LargeUnitName = largeUnit;
        item.UnitsPerLarge = unitsPerLarge;

        var unitsChanged =
            !string.Equals(oldBase, baseUnit, StringComparison.Ordinal) ||
            !string.Equals(oldLarge, largeUnit, StringComparison.Ordinal) ||
            oldFactor != unitsPerLarge;

        if (unitsChanged)
        {
            _db.ItemUnitConversionLogs.Add(new ItemUnitConversionLog
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = item.Id,
                OldBaseUnitName = oldBase,
                NewBaseUnitName = baseUnit,
                OldLargeUnitName = oldLarge,
                NewLargeUnitName = largeUnit,
                OldUnitsPerLarge = oldFactor,
                NewUnitsPerLarge = unitsPerLarge,
                ChangedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Updated", "CafeteriaItem", item.Id, new
        {
            item.Name,
            item.IsActive,
            UnitsChanged = unitsChanged,
            Old = new { Base = oldBase, Large = oldLarge, Factor = oldFactor },
            New = new { Base = baseUnit, Large = largeUnit, Factor = unitsPerLarge }
        }, ct: ct);
        await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    private async Task<(string BaseUnit, string? LargeUnit, int UnitsPerLarge)> ResolveUnitsAsync(
        Guid baseUnitId,
        Guid? largeUnitId,
        int unitsPerLarge,
        CancellationToken ct)
    {
        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);

        var baseUnit = await _db.InventoryUnits.FirstOrDefaultAsync(
            u => u.Id == baseUnitId && u.IsActive && u.OwnerUserId == ownerId, ct)
            ?? throw new InvalidOperationException("Base unit not found. Add it from Inventory → Units first.");

        string? largeName = null;
        if (largeUnitId.HasValue)
        {
            if (largeUnitId.Value == baseUnitId)
                throw new InvalidOperationException("Large unit must be different from the base unit.");

            var large = await _db.InventoryUnits.FirstOrDefaultAsync(
                u => u.Id == largeUnitId.Value && u.IsActive && u.OwnerUserId == ownerId, ct)
                ?? throw new InvalidOperationException("Large unit not found.");
            largeName = large.Name;
        }

        var baseName = baseUnit.Name;
        ItemUnitHelper.NormalizeUnits(ref baseName, ref largeName, ref unitsPerLarge);
        return (baseName, largeName, unitsPerLarge);
    }

    /// <summary>
    /// Branch must be selected and, for non-SuperAdmin, belong to the business owner
    /// (blocks masters from reading another master's items via a stray UserBranch).
    /// </summary>
    private async Task<Guid> RequireOwnedBranchIdAsync(CancellationToken ct)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        if (_tenantContext.IsSuperAdmin)
            return branchId;

        var businessOwnerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        var branchOwnerId = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.OwnerUserId)
            .FirstOrDefaultAsync(ct);

        if (branchOwnerId.HasValue && branchOwnerId.Value != businessOwnerId)
            throw new UnauthorizedAccessException("You do not have access to this branch.");

        return branchId;
    }

    public async Task SoftDeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        item.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        item.IsActive = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.SoftDeleted", "CafeteriaItem", item.Id, new { item.Name }, ct: ct);
    }

    public async Task<IReadOnlyList<CafeteriaSaleDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        var query = _db.CafeteriaSales
            .Include(s => s.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.SoldByUser)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .Where(s => s.BranchId == branchId && s.SessionId == null);

        if (from.HasValue) query = query.Where(s => s.SoldAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SoldAt <= to.Value);

        var sales = await query.OrderByDescending(s => s.SoldAt).ToListAsync(ct);
        return sales.Select(MapSale).ToList();
    }

    public async Task<CafeteriaSaleDto?> GetSaleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        var sale = await _db.CafeteriaSales
            .Include(s => s.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.SoldByUser)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        return sale is null ? null : MapSale(sale);
    }

    public async Task<CafeteriaSaleDto> CreateStandaloneSaleAsync(CreateCafeteriaSaleRequest request, CancellationToken ct = default)
    {
        var branchId = await RequireOwnedBranchIdAsync(ct);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one sale line is required.");

        var sale = new CafeteriaSale
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            SoldByUserId = _tenantContext.UserId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
            SoldAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var line in request.Lines)
        {
            var item = await _db.CafeteriaItems.FirstOrDefaultAsync(
                i => i.Id == line.CafeteriaItemId && i.BranchId == branchId && i.IsActive, ct)
                ?? throw new KeyNotFoundException($"Cafeteria item {line.CafeteriaItemId} not found.");

            var baseQty = ItemUnitHelper.ToBaseQuantity(item, line.Quantity, line.Unit);
            if (item.CurrentQuantity < baseQty)
                throw new InvalidOperationException($"Insufficient stock for {item.Name}.");

            var unitPrice = item.SellPrice;
            var lineTotal = unitPrice * baseQty;
            sale.Lines.Add(new CafeteriaSaleLine
            {
                CafeteriaItemId = item.Id,
                Quantity = baseQty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });

            item.CurrentQuantity -= baseQty;
            total += lineTotal;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = item.Id,
                MovementType = InventoryMovementType.Sale,
                QuantityChange = -baseQty,
                ReferenceType = "CafeteriaSale",
                ReferenceId = sale.Id,
                Notes = line.Unit == InventoryUnitKind.Large
                    ? $"Sold {line.Quantity} {item.LargeUnitName}"
                    : null,
                PerformedByUserId = _tenantContext.UserId
            });

            await _lowStock.CheckAndNotifyAsync(item, ct);
        }

        sale.TotalAmount = total;
        _db.CafeteriaSales.Add(sale);

        var invoice = await _billing.CreateInvoiceAsync(
            branchId, InvoiceType.Cafeteria, null, sale.Id, total, request.Payment, RevenueType.Cafeteria, ct);
        sale.Invoice = invoice;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaSale.Created", "CafeteriaSale", sale.Id, new { total, LineCount = request.Lines.Count }, ct: ct);

        await _db.Entry(sale).Reference(s => s.SoldByUser).LoadAsync(ct);
        await _db.Entry(sale).Collection(s => s.Lines).Query().Include(l => l.CafeteriaItem).LoadAsync(ct);
        await _db.Entry(sale).Reference(s => s.Invoice).Query().Include(i => i!.Payments).LoadAsync(ct);

        return MapSale(sale);
    }

    public async Task<CafeteriaReturnDto> ReturnItemAsync(Guid saleId, ReturnCafeteriaItemRequest request, CancellationToken ct = default)
    {
        _ = saleId;
        _ = request;
        _ = ct;
        throw new InvalidOperationException(
            "Walk-in cafeteria sales cannot be returned. Returns are only allowed for open session customers.");
    }

    private static CafeteriaItemDto MapItem(CafeteriaItem i) =>
        new(i.Id, i.BranchId, i.Name, i.NameAr, i.SellPrice, i.CurrentQuantity, i.MinThreshold,
            i.CurrentQuantity <= i.MinThreshold, i.IsActive,
            i.BaseUnitName, i.LargeUnitName, i.UnitsPerLarge, i.CreatedAt);

    private static CafeteriaSaleDto MapSale(CafeteriaSale s)
    {
        CafeteriaSaleInvoiceDto? invoiceDto = null;
        if (s.Invoice is not null)
        {
            var payment = s.Invoice.Payments.FirstOrDefault();
            invoiceDto = new CafeteriaSaleInvoiceDto(
                s.Invoice.Id,
                s.Invoice.InvoiceNumber,
                s.Invoice.Total,
                payment?.PaymentMethod ?? PaymentMethod.Cash,
                payment?.Status ?? PaymentStatus.Completed);
        }

        return new CafeteriaSaleDto(
            s.Id,
            s.BranchId,
            s.SessionId,
            s.CustomerName,
            s.TotalAmount,
            s.Status,
            s.SoldAt,
            s.SoldByUser.FullName,
            s.Lines.Select(l => new CafeteriaSaleLineDto(
                l.Id, l.CafeteriaItemId, l.CafeteriaItem.Name, l.Quantity, l.ReturnedQuantity,
                l.UnitPrice, l.LineTotal)).ToList(),
            invoiceDto);
    }
}
