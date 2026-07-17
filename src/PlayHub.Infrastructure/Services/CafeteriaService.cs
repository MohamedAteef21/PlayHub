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
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var items = await _db.CafeteriaItems
            .Include(i => i.Variants)
            .Where(i => i.BranchId == branchId)
            .OrderBy(i => i.Name)
            .ToListAsync(ct);
        return items.Select(MapItem).ToList();
    }

    public async Task<CafeteriaItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var item = await _db.CafeteriaItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct);
        return item is null ? null : MapItem(item);
    }

    public async Task<CafeteriaItemDto> CreateItemAsync(CreateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var variants = NormalizeVariants(request.Variants);

        var stock = Math.Max(0, request.CurrentQuantity);
        var minPrice = variants.Min(v => v.SellPrice);

        var item = new CafeteriaItem
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
            SellPrice = minPrice,
            CurrentQuantity = stock,
            MinThreshold = Math.Max(0, request.MinThreshold),
            BaseUnitName = "قطعة",
            LargeUnitName = null,
            UnitsPerLarge = 1
        };

        foreach (var v in variants)
        {
            item.Variants.Add(new CafeteriaItemVariant
            {
                Name = v.Name,
                SellPrice = v.SellPrice,
                IsActive = v.IsActive,
                SortOrder = v.SortOrder
            });
        }

        _db.CafeteriaItems.Add(item);

        if (stock > 0)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = item.Id,
                MovementType = InventoryMovementType.InitialStock,
                QuantityChange = stock,
                ReferenceType = "CafeteriaItem",
                ReferenceId = item.Id,
                Notes = "Initial stock",
                PerformedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Created", "CafeteriaItem", item.Id,
            new { item.Name, VariantCount = item.Variants.Count, stock }, ct: ct);
        await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    public async Task<CafeteriaItemDto> UpdateItemAsync(Guid id, UpdateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var item = await _db.CafeteriaItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        var variants = NormalizeVariants(request.Variants);

        item.Name = request.Name.Trim();
        item.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
        item.MinThreshold = Math.Max(0, request.MinThreshold);
        item.IsActive = request.IsActive;
        item.SellPrice = variants.Min(v => v.SellPrice);
        item.BaseUnitName = "قطعة";
        item.LargeUnitName = null;
        item.UnitsPerLarge = 1;

        ReplaceVariants(item, variants);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Updated", "CafeteriaItem", item.Id,
            new { item.Name, item.IsActive, VariantCount = item.Variants.Count }, ct: ct);
        await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    public async Task SoftDeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        item.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        item.IsActive = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.SoftDeleted", "CafeteriaItem", item.Id, new { item.Name }, ct: ct);
    }

    public async Task<IReadOnlyList<CafeteriaSaleDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var query = _db.CafeteriaSales
            .Include(s => s.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.SoldByUser)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .Where(s => s.BranchId == branchId && s.SessionId == null);

        if (from.HasValue)
            query = query.Where(s => s.SoldAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.SoldAt <= to.Value);

        var sales = await query.OrderByDescending(s => s.SoldAt).ToListAsync(ct);
        return sales.Select(MapSale).ToList();
    }

    public async Task<CafeteriaSaleDto?> GetSaleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var sale = await _db.CafeteriaSales
            .Include(s => s.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.SoldByUser)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        return sale is null ? null : MapSale(sale);
    }

    public async Task<CafeteriaSaleDto> CreateStandaloneSaleAsync(CreateCafeteriaSaleRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

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
            var (item, variant, qty, stockDeduct) = await ResolveSaleLineAsync(branchId, line.CafeteriaItemId, line.VariantId, line.Quantity, line.StockDeductQuantity, ct);

            var unitPrice = variant.SellPrice;
            var lineTotal = unitPrice * qty;
            sale.Lines.Add(new CafeteriaSaleLine
            {
                CafeteriaItemId = item.Id,
                VariantId = variant.Id,
                VariantName = variant.Name,
                Quantity = qty,
                StockDeductQuantity = stockDeduct,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });

            item.CurrentQuantity -= stockDeduct;
            total += lineTotal;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = item.Id,
                MovementType = InventoryMovementType.Sale,
                QuantityChange = -stockDeduct,
                ReferenceType = "CafeteriaSale",
                ReferenceId = sale.Id,
                Notes = $"{item.Name} — {variant.Name}; sold {qty}, stock -{stockDeduct}",
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

    public Task<CafeteriaReturnDto> ReturnItemAsync(Guid saleId, ReturnCafeteriaItemRequest request, CancellationToken ct = default)
    {
        _ = saleId;
        _ = request;
        _ = ct;
        throw new InvalidOperationException(
            "Walk-in cafeteria sales cannot be returned. Returns are only allowed for open session customers.");
    }

    internal async Task<(CafeteriaItem Item, CafeteriaItemVariant Variant, int Quantity, int StockDeduct)> ResolveSaleLineAsync(
        Guid branchId,
        Guid itemId,
        Guid variantId,
        int quantity,
        int stockDeduct,
        CancellationToken ct)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be at least 1.");
        if (stockDeduct <= 0)
            throw new InvalidOperationException("Stock deduct quantity must be at least 1.");

        var item = await _db.CafeteriaItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.BranchId == branchId && i.IsActive, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        var variant = item.Variants.FirstOrDefault(v => v.Id == variantId && v.IsActive)
            ?? throw new KeyNotFoundException("Variant not found for this item.");

        if (item.CurrentQuantity < stockDeduct)
            throw new InvalidOperationException($"Insufficient stock for {item.Name}. Available: {item.CurrentQuantity}.");

        return (item, variant, quantity, stockDeduct);
    }

    private static List<UpsertCafeteriaItemVariantRequest> NormalizeVariants(IReadOnlyList<UpsertCafeteriaItemVariantRequest>? variants)
    {
        if (variants is null || variants.Count == 0)
            throw new InvalidOperationException("At least one variant (name + price) is required.");

        var list = new List<UpsertCafeteriaItemVariantRequest>();
        var order = 0;
        foreach (var v in variants)
        {
            var name = v.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Variant name is required.");
            if (v.SellPrice < 0)
                throw new InvalidOperationException("Variant price cannot be negative.");

            list.Add(new UpsertCafeteriaItemVariantRequest(v.Id, name, v.SellPrice, v.IsActive, order++));
        }

        return list;
    }

    private void ReplaceVariants(CafeteriaItem item, List<UpsertCafeteriaItemVariantRequest> variants)
    {
        var keepIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var orphan in item.Variants.Where(v => !keepIds.Contains(v.Id)))
            orphan.IsActive = false;

        foreach (var v in variants)
        {
            if (v.Id.HasValue)
            {
                var existing = item.Variants.FirstOrDefault(x => x.Id == v.Id.Value);
                if (existing is null)
                {
                    item.Variants.Add(new CafeteriaItemVariant
                    {
                        Name = v.Name,
                        SellPrice = v.SellPrice,
                        IsActive = v.IsActive,
                        SortOrder = v.SortOrder
                    });
                }
                else
                {
                    existing.Name = v.Name;
                    existing.SellPrice = v.SellPrice;
                    existing.IsActive = v.IsActive;
                    existing.SortOrder = v.SortOrder;
                }
            }
            else
            {
                item.Variants.Add(new CafeteriaItemVariant
                {
                    Name = v.Name,
                    SellPrice = v.SellPrice,
                    IsActive = v.IsActive,
                    SortOrder = v.SortOrder
                });
            }
        }
    }

    private static CafeteriaItemDto MapItem(CafeteriaItem i) =>
        new(
            i.Id,
            i.BranchId,
            i.Name,
            i.NameAr,
            i.SellPrice,
            i.CurrentQuantity,
            i.MinThreshold,
            i.CurrentQuantity <= i.MinThreshold,
            i.IsActive,
            i.BaseUnitName,
            i.LargeUnitName,
            i.UnitsPerLarge,
            i.CreatedAt,
            i.Variants
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Name)
                .Select(v => new CafeteriaItemVariantDto(v.Id, v.Name, v.SellPrice, v.IsActive, v.SortOrder))
                .ToList());

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
                l.Id,
                l.CafeteriaItemId,
                l.VariantName is null ? l.CafeteriaItem.Name : $"{l.CafeteriaItem.Name} — {l.VariantName}",
                l.VariantId,
                l.VariantName,
                l.Quantity,
                l.StockDeductQuantity > 0 ? l.StockDeductQuantity : l.Quantity,
                l.ReturnedQuantity,
                l.UnitPrice,
                l.LineTotal)).ToList(),
            invoiceDto);
    }
}
