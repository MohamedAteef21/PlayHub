using PlayHub.Application.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Cafeteria;

public record RecipeLineDto(
    Guid Id,
    Guid WarehouseItemId,
    string WarehouseItemName,
    int Quantity,
    int AvailableQuantity);

public record UpsertRecipeLineRequest(
    Guid? Id,
    Guid WarehouseItemId,
    int Quantity);

public record CafeteriaItemVariantDto(
    Guid Id,
    string Name,
    decimal SellPrice,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<RecipeLineDto> RecipeLines);

public record UpsertCafeteriaItemVariantRequest(
    Guid? Id,
    string Name,
    decimal SellPrice,
    bool IsActive = true,
    int SortOrder = 0,
    IReadOnlyList<UpsertRecipeLineRequest>? RecipeLines = null);

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
    CafeteriaItemKind Kind,
    string BaseUnitName,
    string? LargeUnitName,
    int UnitsPerLarge,
    DateTime CreatedAt,
    IReadOnlyList<CafeteriaItemVariantDto> Variants);

public record CreateCafeteriaItemRequest(
    string Name,
    CafeteriaItemKind Kind,
    string? NameAr = null,
    int CurrentQuantity = 0,
    int MinThreshold = 0,
    IReadOnlyList<UpsertCafeteriaItemVariantRequest>? Variants = null,
    decimal SellPrice = 0,
    Guid? BaseUnitId = null,
    Guid? LargeUnitId = null,
    int UnitsPerLarge = 1,
    InventoryUnitKind InitialStockUnit = InventoryUnitKind.Base);

public record UpdateCafeteriaItemRequest(
    string Name,
    CafeteriaItemKind Kind,
    string? NameAr,
    int MinThreshold,
    bool IsActive,
    IReadOnlyList<UpsertCafeteriaItemVariantRequest>? Variants = null,
    decimal SellPrice = 0,
    Guid? BaseUnitId = null,
    Guid? LargeUnitId = null,
    int UnitsPerLarge = 1);

public record CafeteriaAddOnDto(
    Guid Id,
    Guid BranchId,
    string Name,
    decimal SellPrice,
    Guid WarehouseItemId,
    string WarehouseItemName,
    int DeductQuantity,
    int AvailableQuantity,
    bool IsActive,
    DateTime CreatedAt);

public record CreateCafeteriaAddOnRequest(
    string Name,
    decimal SellPrice,
    Guid WarehouseItemId,
    int DeductQuantity = 1);

public record UpdateCafeteriaAddOnRequest(
    string Name,
    decimal SellPrice,
    Guid WarehouseItemId,
    int DeductQuantity,
    bool IsActive);

public record CafeteriaSaleLineAddOnInput(
    Guid AddOnId,
    int Quantity);

public record CafeteriaSaleLineInput(
    Guid CafeteriaItemId,
    Guid VariantId,
    int Quantity,
    /// <summary>For SellAsIs: stock to deduct (base units). Ignored for recipe menu items (auto from recipe).</summary>
    int StockDeductQuantity = 0,
    InventoryUnitKind Unit = InventoryUnitKind.Base,
    IReadOnlyList<CafeteriaSaleLineAddOnInput>? AddOns = null);

public record CreateCafeteriaSaleRequest(
    IReadOnlyList<CafeteriaSaleLineInput> Lines,
    PaymentRequest Payment,
    string? CustomerName = null,
    bool AllowSkipMissingIngredients = false);

public record CreateCafeteriaHoldRequest(
    IReadOnlyList<CafeteriaSaleLineInput> Lines,
    string? GuestName = null,
    Guid? CustomerId = null,
    bool AllowSkipMissingIngredients = false);

public record AttachHoldToSessionRequest(Guid SessionId);

public record ConvertHoldToSaleRequest(
    PaymentRequest Payment,
    string? CustomerName = null);

public record CafeteriaHoldLineAddOnDto(
    Guid Id,
    Guid AddOnId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    int StockDeductQuantity);

public record CafeteriaHoldLineDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    Guid? VariantId,
    string? VariantName,
    int Quantity,
    int StockDeductQuantity,
    decimal UnitPrice,
    decimal LineTotal,
    IReadOnlyList<CafeteriaHoldLineAddOnDto> AddOns);

public record CafeteriaHoldDto(
    Guid Id,
    Guid BranchId,
    string? GuestName,
    Guid? CustomerId,
    string? CustomerName,
    CafeteriaHoldStatus Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    string CreatedByName,
    Guid? AttachedSessionId,
    Guid? ConvertedSaleId,
    DateTime? FinalizedAt,
    IReadOnlyList<CafeteriaHoldLineDto> Lines);

public record CafeteriaSaleLineAddOnDto(
    Guid Id,
    Guid AddOnId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    int StockDeductQuantity);

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
    decimal LineTotal,
    IReadOnlyList<CafeteriaSaleLineAddOnDto> AddOns);

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

public record MissingIngredientDto(
    Guid WarehouseItemId,
    string Name,
    int Required,
    int Available);

public class MissingIngredientsException : InvalidOperationException
{
    public const string ErrorCode = "MISSING_INGREDIENTS";

    public IReadOnlyList<MissingIngredientDto> Missing { get; }

    public MissingIngredientsException(IReadOnlyList<MissingIngredientDto> missing)
        : base(BuildMessage(missing))
    {
        Missing = missing;
    }

    private static string BuildMessage(IReadOnlyList<MissingIngredientDto> missing)
    {
        var parts = missing.Select(m => $"{m.Name} (مطلوب {m.Required} / متاح {m.Available})");
        return "مكونات ناقصة أو غير كافية: " + string.Join("، ", parts);
    }
}
