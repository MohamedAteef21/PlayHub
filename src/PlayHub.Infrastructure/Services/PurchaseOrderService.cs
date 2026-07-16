using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.PurchaseOrders;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;
    private readonly LowStockNotifier _lowStock;

    public PurchaseOrderService(
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

    public async Task<PagedResult<PurchaseOrderDto>> GetAllAsync(
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.PurchaseOrders
            .Include(o => o.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(o => o.CreatedByUser)
            .Include(o => o.Expense)
            .Where(o => o.BranchId == branchId);

        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<PurchaseOrderDto>(orders.Select(MapOrder).ToList(), total, p, size);
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var order = await _db.PurchaseOrders
            .Include(o => o.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(o => o.CreatedByUser)
            .Include(o => o.Expense)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

        return order is null ? null : MapOrder(order);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one line is required.");

        var order = new PurchaseOrder
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            SupplierName = request.SupplierName?.Trim(),
            Status = PurchaseOrderStatus.Draft,
            CreatedByUserId = _tenantContext.UserId
        };

        decimal total = 0;
        foreach (var line in request.Lines)
        {
            var item = await _db.CafeteriaItems.FirstOrDefaultAsync(
                i => i.Id == line.CafeteriaItemId && i.BranchId == branchId, ct)
                ?? throw new KeyNotFoundException($"Item {line.CafeteriaItemId} not found.");

            order.Lines.Add(new PurchaseOrderLine
            {
                CafeteriaItemId = item.Id,
                OrderedQuantity = line.OrderedQuantity,
                UnitCost = line.UnitCost
            });
            total += line.OrderedQuantity * line.UnitCost;
        }

        order.TotalCost = total;
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PurchaseOrder.Created", "PurchaseOrder", order.Id, new { order.SupplierName, total }, ct: ct);

        await _db.Entry(order).Reference(o => o.CreatedByUser).LoadAsync(ct);
        await _db.Entry(order).Collection(o => o.Lines).Query().Include(l => l.CafeteriaItem).LoadAsync(ct);

        return MapOrder(order);
    }

    public async Task<PurchaseOrderDto> MarkOrderedAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var order = await LoadOrderAsync(id, branchId, ct);
        if (order.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Only draft orders can be marked as ordered.");

        order.Status = PurchaseOrderStatus.Ordered;
        order.OrderedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PurchaseOrder.Ordered", "PurchaseOrder", order.Id, ct: ct);

        return MapOrder(order);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var order = await LoadOrderAsync(id, branchId, ct);
        if (order.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.Draft))
            throw new InvalidOperationException("This purchase order cannot be received.");

        var receivedAt = DateTime.UtcNow;
        foreach (var line in order.Lines)
        {
            line.ReceivedQuantity = line.OrderedQuantity;
            line.CafeteriaItem.CurrentQuantity += line.OrderedQuantity;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = line.CafeteriaItemId,
                MovementType = InventoryMovementType.PurchaseReceive,
                QuantityChange = line.OrderedQuantity,
                ReferenceType = "PurchaseOrder",
                ReferenceId = order.Id,
                PerformedByUserId = _tenantContext.UserId
            });

            await _lowStock.CheckAndNotifyAsync(line.CafeteriaItem, ct);
        }

        order.Status = PurchaseOrderStatus.Received;
        order.ReceivedAt = receivedAt;
        order.ReceivedByUserId = _tenantContext.UserId;

        var category = await GetOrCreateInventoryCategoryAsync(ct);
        order.Expense = new Expense
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            CategoryId = category.Id,
            PurchaseOrderId = order.Id,
            Amount = order.TotalCost,
            Description = $"Inventory purchase — supplier: {order.SupplierName ?? "N/A"}",
            ExpenseDate = DateOnly.FromDateTime(receivedAt),
            RecordedByUserId = _tenantContext.UserId
        };

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PurchaseOrder.Received", "PurchaseOrder", order.Id, new { order.TotalCost }, ct: ct);

        return MapOrder(order);
    }

    private async Task<PurchaseOrder> LoadOrderAsync(Guid id, Guid branchId, CancellationToken ct)
    {
        return await _db.PurchaseOrders
            .Include(o => o.Lines).ThenInclude(l => l.CafeteriaItem)
            .Include(o => o.CreatedByUser)
            .Include(o => o.Expense)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Purchase order not found.");
    }

    private async Task<ExpenseCategory> GetOrCreateInventoryCategoryAsync(CancellationToken ct)
    {
        var category = await _db.ExpenseCategories
            .FirstOrDefaultAsync(c => c.Name == "Inventory Purchases", ct);

        if (category is not null) return category;

        category = new ExpenseCategory
        {
            TenantId = _tenantContext.TenantId,
            Name = "Inventory Purchases",
            NameAr = "مشتريات المخزون"
        };
        _db.ExpenseCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category;
    }

    private static PurchaseOrderDto MapOrder(PurchaseOrder o) =>
        new(
            o.Id,
            o.BranchId,
            o.SupplierName,
            o.Status,
            o.TotalCost,
            o.OrderedAt,
            o.ReceivedAt,
            o.CreatedByUser.FullName,
            o.Lines.Select(l => new PurchaseOrderLineDto(
                l.Id,
                l.CafeteriaItemId,
                l.CafeteriaItem.Name,
                l.OrderedQuantity,
                l.ReceivedQuantity,
                l.UnitCost,
                l.OrderedQuantity * l.UnitCost)).ToList(),
            o.Expense?.Id);
}
