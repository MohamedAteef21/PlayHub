using PlayHub.Application.Pricing;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Pricing;

public interface IPricingService
{
    Task<IReadOnlyList<PricingPlanDto>> GetPlansAsync(SessionMode? mode = null, CancellationToken ct = default);
    Task<PricingPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<PricingPlanDto> CreatePlanAsync(CreatePricingPlanRequest request, CancellationToken ct = default);
    Task<PricingPlanDto> UpdatePlanAsync(Guid id, UpdatePricingPlanRequest request, CancellationToken ct = default);
    Task SoftDeletePlanAsync(Guid id, CancellationToken ct = default);
}
