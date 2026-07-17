using PlayHub.Application.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Cafeteria;

public record CafeteriaItemVariantDto(
    Guid Id,
    string Name,
    decimal SellPrice,
    bool IsActive,
    int SortOrder);

public record UpsertCafeteriaItemVariantRequest(
    Guid? Id,
    string Name,
    decimal SellPrice,
    bool IsActive = true,
    int SortOrder = 0);

public record CafeteriaItemDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? NameAr,
    decimal SellPrice,
    int CurrentQuantity,
    int MinThreshold,
    bool IsLowStock,
    bool IsActive,
    string BaseUnitName,
    string? LargeUnitName,
    int UnitsPerLarge,
    DateTime CreatedAt,
    IReadOnlyList<CafeteriaItemVariantDto> Variants);

public record CreateCafeteriaItemRequest(
    string Name,
    string? NameAr,
    int CurrentQuantity,
    int MinThreshold,
    IReadOnlyList<UpsertCafeteriaItemVariantRequest> Variants,
    // Legacy optional fields kept so older clients don't break.
    decimal SellPrice = 0,
    Guid? BaseUnitId = null,
    Guid? LargeUnitId = null,
    int UnitsPerLarge = 1,
    InventoryUnitKind InitialStockUnit = InventoryUnitKind.Base);

public record UpdateCafeteriaItemRequest(
    string Name,
    string? NameAr,
    int MinThreshold,
    bool IsActive,
    IReadOnlyList<UpsertCafeteriaItemVariantRequest> Variants,
    decimal SellPrice = 0,
    Guid? BaseUnitId = null,
    Guid? LargeUnitId = null,
    int UnitsPerLarge = 1);

public record CafeteriaSaleLineInput(
    Guid CafeteriaItemId,
    Guid VariantId,
    int Quantity,
    /// <summary>How much stock to deduct from the parent product for this line.</summary>
    int StockDeductQuantity,
    InventoryUnitKind Unit = InventoryUnitKind.Base);

public record CreateCafeteriaSaleRequest(
    IReadOnlyList<CafeteriaSaleLineInput> Lines,
    PaymentRequest Payment,
    string? CustomerName = null);

public record CafeteriaSaleLineDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    Guid? VariantId,
    string? VariantName,
    int Quantity,
    int StockDeductQuantity,
    int ReturnedQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public record CafeteriaSaleDto(
    Guid Id,
    Guid BranchId,
    Guid? SessionId,
    string? CustomerName,
    decimal TotalAmount,
    CafeteriaSaleStatus Status,
    DateTime SoldAt,
    string SoldByName,
    IReadOnlyList<CafeteriaSaleLineDto> Lines,
    CafeteriaSaleInvoiceDto? Invoice);

public record CafeteriaSaleInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    decimal Total,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus);

public record ReturnCafeteriaItemRequest(
    Guid SaleLineId,
    int Quantity,
    string Reason);

public record CafeteriaReturnDto(
    Guid Id,
    Guid SaleId,
    Guid SaleLineId,
    int Quantity,
    string Reason,
    decimal RefundAmount,
    DateTime ReturnedAt);
