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

    public async Task<IReadOnlyList<CafeteriaItemDto>> GetItemsAsync(
        CafeteriaItemKind? kind = null,
        bool forSaleOnly = false,
        CancellationToken ct = default)
    {
        var branchId = BranchGuard.ResolveReadBranchId(_tenantContext);
        var query = _db.CafeteriaItems
            .Include(i => i.Variants).ThenInclude(v => v.RecipeLines).ThenInclude(r => r.WarehouseItem)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(i => i.BranchId == branchId.Value);

        if (kind.HasValue)
            query = query.Where(i => i.Kind == kind.Value);
        if (forSaleOnly)
            query = query.Where(i => i.Kind == CafeteriaItemKind.Menu || i.Kind == CafeteriaItemKind.SellAsIs);

        var items = await query.OrderBy(i => i.Name).ToListAsync(ct);
        return items.Select(MapItem).ToList();
    }

    public async Task<CafeteriaItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var item = await _db.CafeteriaItems
            .Include(i => i.Variants).ThenInclude(v => v.RecipeLines).ThenInclude(r => r.WarehouseItem)
            .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct);
        return item is null ? null : MapItem(item);
    }

    public async Task<CafeteriaItemDto> CreateItemAsync(CreateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.ResolveCreateBranchIdAsync(_db, _tenantContext, request.BranchId, ct);
        var kind = request.Kind;
        var tracksStock = kind is CafeteriaItemKind.Warehouse or CafeteriaItemKind.SellAsIs;

        var (baseUnit, largeUnit, unitsPerLarge) = tracksStock
            ? await ResolveUnitsAsync(request.BaseUnitId, request.LargeUnitId, request.UnitsPerLarge, ct)
            : ("قطعة", (string?)null, 1);

        var variants = kind == CafeteriaItemKind.Warehouse
            ? []
            : NormalizeVariants(request.Variants);

        var stock = 0;
        if (tracksStock && request.CurrentQuantity > 0)
        {
            var temp = new CafeteriaItem
            {
                Name = request.Name.Trim(),
                LargeUnitName = largeUnit,
                UnitsPerLarge = unitsPerLarge
            };
            stock = ItemUnitHelper.ToBaseQuantity(temp, request.CurrentQuantity, request.InitialStockUnit);
        }

        var minPrice = variants.Count > 0 ? variants.Min(v => v.SellPrice) : Math.Max(0, request.SellPrice);

        var item = new CafeteriaItem
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
            SellPrice = minPrice,
            CurrentQuantity = stock,
            MinThreshold = Math.Max(0, request.MinThreshold),
            Kind = kind,
            BaseUnitName = baseUnit,
            LargeUnitName = largeUnit,
            UnitsPerLarge = unitsPerLarge
        };

        foreach (var v in variants)
        {
            var variant = new CafeteriaItemVariant
            {
                Name = v.Name,
                SellPrice = v.SellPrice,
                IsActive = v.IsActive,
                SortOrder = v.SortOrder
            };
            await AttachRecipeLinesAsync(branchId, variant, v.RecipeLines, ct);
            item.Variants.Add(variant);
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
                Notes = request.InitialStockUnit == InventoryUnitKind.Large && largeUnit is not null
                    ? $"Initial stock: {request.CurrentQuantity} {largeUnit} (= {stock} {baseUnit})"
                    : "Initial stock",
                PerformedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Created", "CafeteriaItem", item.Id,
            new { item.Name, item.Kind, VariantCount = item.Variants.Count, stock }, ct: ct);
        if (tracksStock)
            await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return await GetItemByIdAsync(item.Id, ct) ?? MapItem(item);
    }

    public async Task<CafeteriaItemDto> UpdateItemAsync(Guid id, UpdateCafeteriaItemRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var item = await _db.CafeteriaItems
            .Include(i => i.Variants).ThenInclude(v => v.RecipeLines)
            .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        var kind = request.Kind;
        var tracksStock = kind is CafeteriaItemKind.Warehouse or CafeteriaItemKind.SellAsIs;
        var oldBase = item.BaseUnitName;
        var oldLarge = item.LargeUnitName;
        var oldFactor = item.UnitsPerLarge;

        var (baseUnit, largeUnit, unitsPerLarge) = tracksStock
            ? await ResolveUnitsAsync(request.BaseUnitId, request.LargeUnitId, request.UnitsPerLarge, ct)
            : ("قطعة", (string?)null, 1);

        var variants = kind == CafeteriaItemKind.Warehouse
            ? []
            : NormalizeVariants(request.Variants);

        item.Name = request.Name.Trim();
        item.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
        item.MinThreshold = Math.Max(0, request.MinThreshold);
        item.IsActive = request.IsActive;
        item.Kind = kind;
        item.SellPrice = variants.Count > 0 ? variants.Min(v => v.SellPrice) : Math.Max(0, request.SellPrice);
        item.BaseUnitName = baseUnit;
        item.LargeUnitName = largeUnit;
        item.UnitsPerLarge = unitsPerLarge;

        if (tracksStock)
        {
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
        }

        await ReplaceVariantsAsync(branchId, item, variants, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaItem.Updated", "CafeteriaItem", item.Id,
            new { item.Name, item.Kind, item.IsActive, VariantCount = item.Variants.Count }, ct: ct);
        if (tracksStock)
            await _lowStock.CheckAndNotifyAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return await GetItemByIdAsync(item.Id, ct) ?? MapItem(item);
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

    public async Task<IReadOnlyList<CafeteriaAddOnDto>> GetAddOnsAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var query = _db.CafeteriaAddOns
            .Include(a => a.WarehouseItem)
            .Where(a => a.BranchId == branchId);
        if (activeOnly)
            query = query.Where(a => a.IsActive);

        var list = await query.OrderBy(a => a.Name).ToListAsync(ct);
        return list.Select(MapAddOn).ToList();
    }

    public async Task<CafeteriaAddOnDto> CreateAddOnAsync(CreateCafeteriaAddOnRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.ResolveCreateBranchIdAsync(_db, _tenantContext, request.BranchId, ct);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Add-on name is required.");
        if (request.SellPrice < 0)
            throw new InvalidOperationException("Add-on price cannot be negative.");
        if (request.DeductQuantity <= 0)
            throw new InvalidOperationException("Deduct quantity must be at least 1.");

        var warehouse = await RequireWarehouseItemAsync(branchId, request.WarehouseItemId, ct);

        var addOn = new CafeteriaAddOn
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            SellPrice = request.SellPrice,
            WarehouseItemId = warehouse.Id,
            DeductQuantity = request.DeductQuantity,
            IsActive = true
        };
        _db.CafeteriaAddOns.Add(addOn);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaAddOn.Created", "CafeteriaAddOn", addOn.Id, new { addOn.Name }, ct: ct);

        await _db.Entry(addOn).Reference(a => a.WarehouseItem).LoadAsync(ct);
        return MapAddOn(addOn);
    }

    public async Task<CafeteriaAddOnDto> UpdateAddOnAsync(Guid id, UpdateCafeteriaAddOnRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var addOn = await _db.CafeteriaAddOns
            .Include(a => a.WarehouseItem)
            .FirstOrDefaultAsync(a => a.Id == id && a.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Add-on not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Add-on name is required.");
        if (request.SellPrice < 0)
            throw new InvalidOperationException("Add-on price cannot be negative.");
        if (request.DeductQuantity <= 0)
            throw new InvalidOperationException("Deduct quantity must be at least 1.");

        var warehouse = await RequireWarehouseItemAsync(branchId, request.WarehouseItemId, ct);
        addOn.Name = request.Name.Trim();
        addOn.SellPrice = request.SellPrice;
        addOn.WarehouseItemId = warehouse.Id;
        addOn.DeductQuantity = request.DeductQuantity;
        addOn.IsActive = request.IsActive;
        addOn.WarehouseItem = warehouse;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaAddOn.Updated", "CafeteriaAddOn", addOn.Id, new { addOn.Name, addOn.IsActive }, ct: ct);
        return MapAddOn(addOn);
    }

    public async Task SoftDeleteAddOnAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var addOn = await _db.CafeteriaAddOns.FirstOrDefaultAsync(a => a.Id == id && a.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Add-on not found.");

        addOn.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        addOn.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaAddOn.SoftDeleted", "CafeteriaAddOn", addOn.Id, new { addOn.Name }, ct: ct);
    }

    public async Task<IReadOnlyList<CafeteriaSaleDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var query = _db.CafeteriaSales
            .Include(s => s.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.Lines).ThenInclude(l => l.AddOns)
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
            .Include(s => s.Lines).ThenInclude(l => l.AddOns)
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
        var touched = new HashSet<Guid>();

        foreach (var line in request.Lines)
        {
            var plan = await CafeteriaStockPlanner.PlanAsync(
                _db, branchId, line.CafeteriaItemId, line.VariantId, line.Quantity,
                line.StockDeductQuantity, line.Unit, line.AddOns, request.AllowSkipMissingIngredients, ct);

            var saleLine = new CafeteriaSaleLine
            {
                CafeteriaItemId = plan.Item.Id,
                VariantId = plan.Variant.Id,
                VariantName = plan.Variant.Name,
                Quantity = plan.Quantity,
                StockDeductQuantity = plan.ParentStockDeduct,
                UnitPrice = plan.UnitPrice,
                LineTotal = plan.LineTotal
            };

            foreach (var a in plan.AddOns)
            {
                saleLine.AddOns.Add(new CafeteriaSaleLineAddOn
                {
                    AddOnId = a.AddOn.Id,
                    Name = a.AddOn.Name,
                    Quantity = a.Quantity,
                    UnitPrice = a.AddOn.SellPrice,
                    LineTotal = a.LineTotal,
                    StockDeductQuantity = a.StockDeduct
                });
            }

            sale.Lines.Add(saleLine);
            total += plan.LineTotal;

            CafeteriaStockPlanner.ApplyDeducts(
                _db, _tenantContext, branchId, plan, "CafeteriaSale", sale.Id,
                trackSaleIngredient: d => saleLine.IngredientDeducts.Add(d));

            touched.Add(plan.Item.Id);
            foreach (var ing in plan.Ingredients)
                touched.Add(ing.WarehouseItem.Id);
        }

        sale.TotalAmount = total;
        _db.CafeteriaSales.Add(sale);

        var invoice = await _billing.CreateInvoiceAsync(
            branchId, InvoiceType.Cafeteria, null, sale.Id, total, request.Payment, RevenueType.Cafeteria, ct);
        sale.Invoice = invoice;

        await _db.SaveChangesAsync(ct);

        foreach (var itemId in touched)
        {
            var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is not null)
                await _lowStock.CheckAndNotifyAsync(item, ct);
        }

        await _audit.LogAsync("CafeteriaSale.Created", "CafeteriaSale", sale.Id, new { total, LineCount = request.Lines.Count }, ct: ct);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(sale).Reference(s => s.SoldByUser).LoadAsync(ct);
        await _db.Entry(sale).Collection(s => s.Lines).Query()
            .Include(l => l.CafeteriaItem)
            .Include(l => l.AddOns)
            .LoadAsync(ct);
        await _db.Entry(sale).Reference(s => s.Invoice).Query().Include(i => i!.Payments).LoadAsync(ct);

        return MapSale(sale);
    }

    public async Task<IReadOnlyList<CafeteriaHoldDto>> GetOpenHoldsAsync(CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var holds = await _db.CafeteriaHolds
            .Include(h => h.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(h => h.Lines).ThenInclude(l => l.AddOns)
            .Include(h => h.CreatedByUser)
            .Include(h => h.Customer)
            .Where(h => h.BranchId == branchId && h.Status == CafeteriaHoldStatus.Open)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return holds.Select(MapHold).ToList();
    }

    public async Task<CafeteriaHoldDto> CreateHoldAsync(CreateCafeteriaHoldRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one hold line is required.");

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && c.IsActive, ct)
                ?? throw new KeyNotFoundException("Customer not found.");
        }

        var hold = new CafeteriaHold
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            GuestName = string.IsNullOrWhiteSpace(request.GuestName) ? null : request.GuestName.Trim(),
            CustomerId = customer?.Id,
            Status = CafeteriaHoldStatus.Open,
            CreatedByUserId = _tenantContext.UserId
        };

        decimal total = 0;
        var touched = new HashSet<Guid>();

        foreach (var line in request.Lines)
        {
            var plan = await CafeteriaStockPlanner.PlanAsync(
                _db, branchId, line.CafeteriaItemId, line.VariantId, line.Quantity,
                line.StockDeductQuantity, line.Unit, line.AddOns, request.AllowSkipMissingIngredients, ct);

            var holdLine = new CafeteriaHoldLine
            {
                CafeteriaItemId = plan.Item.Id,
                VariantId = plan.Variant.Id,
                VariantName = plan.Variant.Name,
                Quantity = plan.Quantity,
                StockDeductQuantity = plan.ParentStockDeduct,
                UnitPrice = plan.UnitPrice,
                LineTotal = plan.LineTotal
            };

            foreach (var a in plan.AddOns)
            {
                holdLine.AddOns.Add(new CafeteriaHoldLineAddOn
                {
                    AddOnId = a.AddOn.Id,
                    Name = a.AddOn.Name,
                    Quantity = a.Quantity,
                    UnitPrice = a.AddOn.SellPrice,
                    LineTotal = a.LineTotal,
                    StockDeductQuantity = a.StockDeduct
                });
            }

            hold.Lines.Add(holdLine);
            total += plan.LineTotal;

            CafeteriaStockPlanner.ApplyDeducts(
                _db, _tenantContext, branchId, plan, "CafeteriaHold", hold.Id,
                trackHoldIngredient: d => holdLine.IngredientDeducts.Add(d),
                holdMode: true);

            touched.Add(plan.Item.Id);
            foreach (var ing in plan.Ingredients)
                touched.Add(ing.WarehouseItem.Id);
        }

        hold.TotalAmount = total;
        _db.CafeteriaHolds.Add(hold);
        await _db.SaveChangesAsync(ct);

        foreach (var itemId in touched)
        {
            var item = await _db.CafeteriaItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is not null)
                await _lowStock.CheckAndNotifyAsync(item, ct);
        }

        await _audit.LogAsync("CafeteriaHold.Created", "CafeteriaHold", hold.Id,
            new { total, LineCount = request.Lines.Count, hold.GuestName }, ct: ct);
        await _db.SaveChangesAsync(ct);

        await LoadHoldGraphAsync(hold, ct);
        return MapHold(hold);
    }

    public async Task<CafeteriaHoldDto> AttachToSessionAsync(Guid holdId, AttachHoldToSessionRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var hold = await _db.CafeteriaHolds
            .Include(h => h.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(h => h.Lines).ThenInclude(l => l.AddOns)
            .Include(h => h.CreatedByUser)
            .Include(h => h.Customer)
            .FirstOrDefaultAsync(h => h.Id == holdId && h.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria hold not found.");

        if (hold.Status != CafeteriaHoldStatus.Open)
            throw new InvalidOperationException("Only open holds can be attached to a session.");

        var session = await _db.Sessions
            .Include(s => s.CafeteriaLines)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found.");

        var customerLabel = hold.GuestName ?? hold.Customer?.Name;

        foreach (var hl in hold.Lines)
        {
            var line = new SessionCafeteriaLine
            {
                CafeteriaItemId = hl.CafeteriaItemId,
                VariantId = hl.VariantId,
                VariantName = hl.VariantName,
                Quantity = hl.Quantity,
                StockDeductQuantity = 0,
                UnitPrice = hl.UnitPrice,
                LineTotal = hl.LineTotal,
                CustomerName = customerLabel,
                AddedByUserId = _tenantContext.UserId
            };

            foreach (var a in hl.AddOns)
            {
                line.AddOns.Add(new SessionCafeteriaLineAddOn
                {
                    AddOnId = a.AddOnId,
                    Name = a.Name,
                    Quantity = a.Quantity,
                    UnitPrice = a.UnitPrice,
                    LineTotal = a.LineTotal,
                    StockDeductQuantity = 0
                });
            }

            session.CafeteriaLines.Add(line);
        }

        hold.Status = CafeteriaHoldStatus.AttachedToSession;
        hold.AttachedSessionId = session.Id;
        hold.FinalizedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaHold.AttachedToSession", "CafeteriaHold", hold.Id,
            new { SessionId = session.Id, hold.TotalAmount }, ct: ct);

        return MapHold(hold);
    }

    public async Task<CafeteriaSaleDto> ConvertToSaleAsync(Guid holdId, ConvertHoldToSaleRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var hold = await _db.CafeteriaHolds
            .Include(h => h.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(h => h.Lines).ThenInclude(l => l.AddOns)
            .Include(h => h.Lines).ThenInclude(l => l.IngredientDeducts)
            .Include(h => h.Customer)
            .FirstOrDefaultAsync(h => h.Id == holdId && h.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria hold not found.");

        if (hold.Status != CafeteriaHoldStatus.Open)
            throw new InvalidOperationException("Only open holds can be converted to a sale.");

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? hold.GuestName ?? hold.Customer?.Name
            : request.CustomerName.Trim();

        var sale = new CafeteriaSale
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            SoldByUserId = _tenantContext.UserId,
            CustomerName = customerName,
            SoldAt = DateTime.UtcNow,
            TotalAmount = hold.TotalAmount
        };

        foreach (var hl in hold.Lines)
        {
            var saleLine = new CafeteriaSaleLine
            {
                CafeteriaItemId = hl.CafeteriaItemId,
                VariantId = hl.VariantId,
                VariantName = hl.VariantName,
                Quantity = hl.Quantity,
                StockDeductQuantity = hl.StockDeductQuantity,
                UnitPrice = hl.UnitPrice,
                LineTotal = hl.LineTotal
            };

            foreach (var a in hl.AddOns)
            {
                saleLine.AddOns.Add(new CafeteriaSaleLineAddOn
                {
                    AddOnId = a.AddOnId,
                    Name = a.Name,
                    Quantity = a.Quantity,
                    UnitPrice = a.UnitPrice,
                    LineTotal = a.LineTotal,
                    StockDeductQuantity = a.StockDeductQuantity
                });
            }

            foreach (var d in hl.IngredientDeducts)
            {
                saleLine.IngredientDeducts.Add(new CafeteriaSaleLineIngredientDeduct
                {
                    WarehouseItemId = d.WarehouseItemId,
                    Quantity = d.Quantity,
                    WasSkipped = d.WasSkipped
                });
            }

            sale.Lines.Add(saleLine);
        }

        _db.CafeteriaSales.Add(sale);

        var payment = request.Payment with
        {
            CustomerId = request.Payment.CustomerId ?? hold.CustomerId
        };

        var invoice = await _billing.CreateInvoiceAsync(
            branchId, InvoiceType.Cafeteria, null, sale.Id, hold.TotalAmount, payment, RevenueType.Cafeteria, ct);
        sale.Invoice = invoice;

        hold.Status = CafeteriaHoldStatus.ConvertedToSale;
        hold.ConvertedSaleId = sale.Id;
        hold.FinalizedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CafeteriaHold.ConvertedToSale", "CafeteriaHold", hold.Id,
            new { SaleId = sale.Id, hold.TotalAmount }, ct: ct);

        await _db.Entry(sale).Reference(s => s.SoldByUser).LoadAsync(ct);
        await _db.Entry(sale).Collection(s => s.Lines).Query()
            .Include(l => l.CafeteriaItem)
            .Include(l => l.AddOns)
            .LoadAsync(ct);
        await _db.Entry(sale).Reference(s => s.Invoice).Query().Include(i => i!.Payments).LoadAsync(ct);

        return MapSale(sale);
    }

    public async Task<CafeteriaHoldDto> CancelHoldAsync(Guid holdId, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var hold = await _db.CafeteriaHolds
            .Include(h => h.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(h => h.Lines).ThenInclude(l => l.AddOns)
            .Include(h => h.Lines).ThenInclude(l => l.IngredientDeducts).ThenInclude(d => d.WarehouseItem)
            .Include(h => h.CreatedByUser)
            .Include(h => h.Customer)
            .FirstOrDefaultAsync(h => h.Id == holdId && h.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Cafeteria hold not found.");

        if (hold.Status != CafeteriaHoldStatus.Open)
            throw new InvalidOperationException("Only open holds can be cancelled.");

        foreach (var line in hold.Lines)
        {
            if (line.StockDeductQuantity > 0)
            {
                line.CafeteriaItem.CurrentQuantity += line.StockDeductQuantity;
                _db.InventoryMovements.Add(new InventoryMovement
                {
                    TenantId = _tenantContext.TenantId,
                    BranchId = branchId,
                    CafeteriaItemId = line.CafeteriaItemId,
                    MovementType = InventoryMovementType.Return,
                    QuantityChange = line.StockDeductQuantity,
                    ReferenceType = "CafeteriaHoldCancel",
                    ReferenceId = hold.Id,
                    Notes = "Hold cancelled — stock restored",
                    PerformedByUserId = _tenantContext.UserId
                });
            }

            foreach (var ded in line.IngredientDeducts.Where(d => !d.WasSkipped && d.Quantity > 0))
            {
                ded.WarehouseItem.CurrentQuantity += ded.Quantity;
                _db.InventoryMovements.Add(new InventoryMovement
                {
                    TenantId = _tenantContext.TenantId,
                    BranchId = branchId,
                    CafeteriaItemId = ded.WarehouseItemId,
                    MovementType = InventoryMovementType.Return,
                    QuantityChange = ded.Quantity,
                    ReferenceType = "CafeteriaHoldCancel",
                    ReferenceId = hold.Id,
                    Notes = "Hold cancelled — ingredient restored",
                    PerformedByUserId = _tenantContext.UserId
                });
            }
        }

        hold.Status = CafeteriaHoldStatus.Cancelled;
        hold.FinalizedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        foreach (var line in hold.Lines)
        {
            await _lowStock.CheckAndNotifyAsync(line.CafeteriaItem, ct);
            foreach (var ded in line.IngredientDeducts.Where(d => !d.WasSkipped))
                await _lowStock.CheckAndNotifyAsync(ded.WarehouseItem, ct);
        }

        await _audit.LogAsync("CafeteriaHold.Cancelled", "CafeteriaHold", hold.Id,
            new { hold.TotalAmount, LineCount = hold.Lines.Count }, ct: ct);
        await _db.SaveChangesAsync(ct);

        return MapHold(hold);
    }

    public Task<CafeteriaReturnDto> ReturnItemAsync(Guid saleId, ReturnCafeteriaItemRequest request, CancellationToken ct = default)
    {
        _ = saleId;
        _ = request;
        _ = ct;
        throw new InvalidOperationException(
            "Walk-in cafeteria sales cannot be returned. Returns are only allowed for open session customers.");
    }

    private async Task<(string BaseUnit, string? LargeUnit, int UnitsPerLarge)> ResolveUnitsAsync(
        Guid? baseUnitId,
        Guid? largeUnitId,
        int unitsPerLarge,
        CancellationToken ct)
    {
        if (!baseUnitId.HasValue)
            throw new InvalidOperationException("Base unit is required for warehouse / sell-as-is items. Add it from Inventory → Units first.");

        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);

        var baseUnit = await _db.InventoryUnits.FirstOrDefaultAsync(
            u => u.Id == baseUnitId.Value && u.IsActive && u.OwnerUserId == ownerId, ct)
            ?? throw new InvalidOperationException("Base unit not found. Add it from Inventory → Units first.");

        string? largeName = null;
        if (largeUnitId.HasValue)
        {
            if (largeUnitId.Value == baseUnitId.Value)
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

    private async Task<CafeteriaItem> RequireWarehouseItemAsync(Guid branchId, Guid warehouseItemId, CancellationToken ct)
    {
        var item = await _db.CafeteriaItems.FirstOrDefaultAsync(
            i => i.Id == warehouseItemId && i.BranchId == branchId && i.IsActive, ct)
            ?? throw new KeyNotFoundException("Warehouse item not found.");

        if (item.Kind is not (CafeteriaItemKind.Warehouse or CafeteriaItemKind.SellAsIs))
            throw new InvalidOperationException("Recipe/add-on stock must come from a warehouse or sell-as-is item.");

        return item;
    }

    private async Task AttachRecipeLinesAsync(
        Guid branchId,
        CafeteriaItemVariant variant,
        IReadOnlyList<UpsertRecipeLineRequest>? lines,
        CancellationToken ct)
    {
        if (lines is null || lines.Count == 0)
            return;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Recipe quantity must be at least 1.");
            await RequireWarehouseItemAsync(branchId, line.WarehouseItemId, ct);
            variant.RecipeLines.Add(new CafeteriaVariantRecipeLine
            {
                WarehouseItemId = line.WarehouseItemId,
                Quantity = line.Quantity
            });
        }
    }

    private static List<UpsertCafeteriaItemVariantRequest> NormalizeVariants(IReadOnlyList<UpsertCafeteriaItemVariantRequest>? variants)
    {
        if (variants is null || variants.Count == 0)
            throw new InvalidOperationException("At least one variant (name + price) is required for sellable products.");

        var list = new List<UpsertCafeteriaItemVariantRequest>();
        var order = 0;
        foreach (var v in variants)
        {
            var name = v.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Variant name is required.");
            if (v.SellPrice < 0)
                throw new InvalidOperationException("Variant price cannot be negative.");

            list.Add(new UpsertCafeteriaItemVariantRequest(
                v.Id, name, v.SellPrice, v.IsActive, order++, v.RecipeLines));
        }

        return list;
    }

    private async Task ReplaceVariantsAsync(
        Guid branchId,
        CafeteriaItem item,
        List<UpsertCafeteriaItemVariantRequest> variants,
        CancellationToken ct)
    {
        var keepIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var orphan in item.Variants.Where(v => !keepIds.Contains(v.Id)).ToList())
        {
            orphan.IsActive = false;
            orphan.RecipeLines.Clear();
        }

        foreach (var v in variants)
        {
            CafeteriaItemVariant target;
            if (v.Id.HasValue)
            {
                var existing = item.Variants.FirstOrDefault(x => x.Id == v.Id.Value);
                if (existing is null)
                {
                    target = new CafeteriaItemVariant();
                    item.Variants.Add(target);
                }
                else
                {
                    target = existing;
                }
            }
            else
            {
                target = new CafeteriaItemVariant();
                item.Variants.Add(target);
            }

            target.Name = v.Name;
            target.SellPrice = v.SellPrice;
            target.IsActive = v.IsActive;
            target.SortOrder = v.SortOrder;
            target.RecipeLines.Clear();
            await AttachRecipeLinesAsync(branchId, target, v.RecipeLines, ct);
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
            i.Kind,
            i.BaseUnitName,
            i.LargeUnitName,
            i.UnitsPerLarge,
            i.CreatedAt,
            i.Variants
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Name)
                .Select(v => new CafeteriaItemVariantDto(
                    v.Id,
                    v.Name,
                    v.SellPrice,
                    v.IsActive,
                    v.SortOrder,
                    v.RecipeLines
                        .Select(r => new RecipeLineDto(
                            r.Id,
                            r.WarehouseItemId,
                            r.WarehouseItem?.Name ?? "",
                            r.Quantity,
                            r.WarehouseItem?.CurrentQuantity ?? 0))
                        .ToList()))
                .ToList());

    private static CafeteriaAddOnDto MapAddOn(CafeteriaAddOn a) =>
        new(
            a.Id,
            a.BranchId,
            a.Name,
            a.SellPrice,
            a.WarehouseItemId,
            a.WarehouseItem?.Name ?? "",
            a.DeductQuantity,
            a.WarehouseItem?.CurrentQuantity ?? 0,
            a.IsActive,
            a.CreatedAt);

    private async Task LoadHoldGraphAsync(CafeteriaHold hold, CancellationToken ct)
    {
        await _db.Entry(hold).Reference(h => h.CreatedByUser).LoadAsync(ct);
        await _db.Entry(hold).Reference(h => h.Customer).LoadAsync(ct);
        await _db.Entry(hold).Collection(h => h.Lines).Query()
            .Include(l => l.CafeteriaItem)
            .Include(l => l.AddOns)
            .LoadAsync(ct);
    }

    private static CafeteriaHoldDto MapHold(CafeteriaHold h) =>
        new(
            h.Id,
            h.BranchId,
            h.GuestName,
            h.CustomerId,
            h.Customer?.Name,
            h.Status,
            h.TotalAmount,
            h.CreatedAt,
            h.CreatedByUser.FullName,
            h.AttachedSessionId,
            h.ConvertedSaleId,
            h.FinalizedAt,
            h.Lines.Select(l => new CafeteriaHoldLineDto(
                l.Id,
                l.CafeteriaItemId,
                l.VariantName is null ? l.CafeteriaItem.Name : $"{l.CafeteriaItem.Name} — {l.VariantName}",
                l.VariantId,
                l.VariantName,
                l.Quantity,
                l.StockDeductQuantity,
                l.UnitPrice,
                l.LineTotal,
                (l.AddOns ?? [])
                    .Select(a => new CafeteriaHoldLineAddOnDto(
                        a.Id, a.AddOnId, a.Name, a.Quantity, a.UnitPrice, a.LineTotal, a.StockDeductQuantity))
                    .ToList())).ToList());

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
                l.StockDeductQuantity,
                l.ReturnedQuantity,
                l.UnitPrice,
                l.LineTotal,
                (l.AddOns ?? [])
                    .Select(a => new CafeteriaSaleLineAddOnDto(
                        a.Id, a.AddOnId, a.Name, a.Quantity, a.UnitPrice, a.LineTotal, a.StockDeductQuantity))
                    .ToList())).ToList(),
            invoiceDto);
    }
}
