namespace PlayHub.Application.Inventory;

public record InventoryUnitDto(
    Guid Id,
    string Name,
    string? NameAr,
    bool IsActive,
    DateTime CreatedAt);

public record CreateInventoryUnitRequest(string Name, string? NameAr = null);

public record UpdateInventoryUnitRequest(string Name, string? NameAr, bool IsActive);

public record ItemUnitConversionLogDto(
    Guid Id,
    Guid CafeteriaItemId,
    string ItemName,
    string OldBaseUnitName,
    string NewBaseUnitName,
    string? OldLargeUnitName,
    string? NewLargeUnitName,
    int OldUnitsPerLarge,
    int NewUnitsPerLarge,
    string ChangedByName,
    DateTime CreatedAt);

public interface IInventoryUnitService
{
    Task<IReadOnlyList<InventoryUnitDto>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<InventoryUnitDto> CreateAsync(CreateInventoryUnitRequest request, CancellationToken ct = default);
    Task<InventoryUnitDto> UpdateAsync(Guid id, UpdateInventoryUnitRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItemUnitConversionLogDto>> GetConversionLogsAsync(
        Guid? itemId = null, int take = 50, CancellationToken ct = default);
}
