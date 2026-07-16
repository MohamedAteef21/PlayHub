using PlayHub.Domain.Enums;

namespace PlayHub.Application.PurchaseOrders;

public record PurchaseOrderLineInput(Guid CafeteriaItemId, int OrderedQuantity, decimal UnitCost);

public record CreatePurchaseOrderRequest(
    string? SupplierName,
    IReadOnlyList<PurchaseOrderLineInput> Lines);

public record PurchaseOrderLineDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    int OrderedQuantity,
    int ReceivedQuantity,
    decimal UnitCost,
    decimal LineTotal);

public record PurchaseOrderDto(
    Guid Id,
    Guid BranchId,
    string? SupplierName,
    PurchaseOrderStatus Status,
    decimal TotalCost,
    DateTime? OrderedAt,
    DateTime? ReceivedAt,
    string CreatedByName,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    Guid? ExpenseId);

public interface IPurchaseOrderService
{
    Task<PlayHub.Application.Common.PagedResult<PurchaseOrderDto>> GetAllAsync(
        int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PurchaseOrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDto> MarkOrderedAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> ReceiveAsync(Guid id, CancellationToken ct = default);
}
