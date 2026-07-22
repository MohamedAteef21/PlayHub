using PlayHub.Application.Cafeteria;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Inventory;

public record InventoryMovementDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    InventoryMovementType MovementType,
    int QuantityChange,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Notes,
    string PerformedByName,
    DateTime CreatedAt);

public record AdjustInventoryRequest(int NewQuantity, string Reason);

public record StockVoucherLineInput(
    Guid CafeteriaItemId,
    int Quantity,
    InventoryUnitKind Unit = InventoryUnitKind.Base,
    string? Notes = null);

public record CreateStockVoucherRequest(
    StockVoucherType VoucherType,
    IReadOnlyList<StockVoucherLineInput> Lines,
    string? Notes = null,
    Guid? RelatedCountVoucherId = null,
    Guid? BranchId = null);

public record StockVoucherLineDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    int Quantity,
    int? SystemQuantity,
    int? Variance,
    int? EnteredQuantity,
    InventoryUnitKind EnteredUnit,
    string? Notes);

public record StockVoucherDto(
    Guid Id,
    Guid BranchId,
    string VoucherNumber,
    StockVoucherType VoucherType,
    StockVoucherStatus Status,
    string? Notes,
    Guid? RelatedCountVoucherId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? PostedAt,
    string? PostedByName,
    IReadOnlyList<StockVoucherLineDto> Lines);

public interface IInventoryService
{
    Task<PlayHub.Application.Common.PagedResult<InventoryMovementDto>> GetMovementsAsync(
        Guid? itemId = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CafeteriaItemDto> AdjustQuantityAsync(Guid itemId, AdjustInventoryRequest request, CancellationToken ct = default);

    Task<PlayHub.Application.Common.PagedResult<StockVoucherDto>> GetVouchersAsync(
        StockVoucherType? type = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<StockVoucherDto?> GetVoucherAsync(Guid id, CancellationToken ct = default);
    Task<StockVoucherDto> CreateVoucherAsync(CreateStockVoucherRequest request, CancellationToken ct = default);
    Task<StockVoucherDto> PostVoucherAsync(Guid id, CancellationToken ct = default);
    Task<StockVoucherDto> CreateSettlementFromCountAsync(Guid countVoucherId, string? notes = null, CancellationToken ct = default);
}
