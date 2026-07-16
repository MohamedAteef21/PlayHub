using PlayHub.Domain.Enums;

namespace PlayHub.Application.Pricing;

public record GamingRateDto(int ControllerCount, decimal Rate);

public record WatchingRateDto(decimal RatePerPerson);

public record PricingPlanDto(
    Guid Id,
    Guid? BranchId,
    string Name,
    SessionMode SessionMode,
    TimeUnit TimeUnit,
    WatchingBilling WatchingBilling,
    int? PackageDurationMinutes,
    decimal? PackagePrice,
    bool IsActive,
    IReadOnlyList<GamingRateDto> GamingRates,
    IReadOnlyList<WatchingRateDto> WatchingRates,
    DateTime CreatedAt);

public record GamingRateInput(int ControllerCount, decimal Rate);

public record WatchingRateInput(decimal RatePerPerson);

public record CreatePricingPlanRequest(
    string Name,
    SessionMode SessionMode,
    TimeUnit TimeUnit,
    WatchingBilling WatchingBilling,
    Guid? BranchId,
    IReadOnlyList<GamingRateInput>? GamingRates,
    IReadOnlyList<WatchingRateInput>? WatchingRates,
    int? PackageDurationMinutes = null,
    decimal? PackagePrice = null);

public record UpdatePricingPlanRequest(
    string Name,
    TimeUnit TimeUnit,
    WatchingBilling WatchingBilling,
    bool IsActive,
    IReadOnlyList<GamingRateInput>? GamingRates,
    IReadOnlyList<WatchingRateInput>? WatchingRates,
    int? PackageDurationMinutes = null,
    decimal? PackagePrice = null);
