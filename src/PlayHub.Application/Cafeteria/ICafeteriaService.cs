using PlayHub.Application.Cafeteria;
using PlayHub.Application.Common;

namespace PlayHub.Application.Cafeteria;

public interface ICafeteriaService
{
    Task<IReadOnlyList<CafeteriaItemDto>> GetItemsAsync(CancellationToken ct = default);
    Task<CafeteriaItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<CafeteriaItemDto> CreateItemAsync(CreateCafeteriaItemRequest request, CancellationToken ct = default);
    Task<CafeteriaItemDto> UpdateItemAsync(Guid id, UpdateCafeteriaItemRequest request, CancellationToken ct = default);
    Task SoftDeleteItemAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CafeteriaSaleDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<CafeteriaSaleDto?> GetSaleByIdAsync(Guid id, CancellationToken ct = default);
    Task<CafeteriaSaleDto> CreateStandaloneSaleAsync(CreateCafeteriaSaleRequest request, CancellationToken ct = default);
    Task<CafeteriaReturnDto> ReturnItemAsync(Guid saleId, ReturnCafeteriaItemRequest request, CancellationToken ct = default);
}
