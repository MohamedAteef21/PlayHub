namespace PlayHub.Application.Offers;

public record OfferDto(
    Guid Id,
    string Title,
    string Message,
    bool IsActive,
    DateTime CreatedAt);

public record CreateOfferRequest(string Title, string Message, bool IsActive = true);

public record UpdateOfferRequest(string Title, string Message, bool IsActive);

public interface IOfferService
{
    Task<IReadOnlyList<OfferDto>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task<OfferDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OfferDto> CreateAsync(CreateOfferRequest request, CancellationToken ct = default);
    Task<OfferDto> UpdateAsync(Guid id, UpdateOfferRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
