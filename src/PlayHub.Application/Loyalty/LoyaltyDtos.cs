using PlayHub.Application.Common;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Loyalty;

public record LoyaltyOfferConditionDto(
    Guid Id,
    LoyaltyConditionMetric Metric,
    decimal RequiredQuantity,
    int? WindowDays,
    Guid? CafeteriaItemId,
    string? CafeteriaItemName,
    Guid? VariantId,
    string? VariantName);

public record LoyaltyOfferRewardDto(
    Guid Id,
    LoyaltyRewardMetric Metric,
    decimal Quantity,
    Guid? CafeteriaItemId,
    string? CafeteriaItemName,
    Guid? VariantId,
    string? VariantName);

public record LoyaltyOfferDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    LoyaltyPlayerScope PlayerScope,
    LoyaltyFulfillment Fulfillment,
    LoyaltyConditionLogic ConditionLogic,
    IReadOnlyList<LoyaltyOfferConditionDto> Conditions,
    IReadOnlyList<LoyaltyOfferRewardDto> Rewards,
    IReadOnlyList<Guid> DeviceIds,
    IReadOnlyList<string> DeviceNames,
    DateTime CreatedAt);

public record UpsertLoyaltyOfferConditionRequest(
    LoyaltyConditionMetric Metric,
    decimal RequiredQuantity,
    int? WindowDays = null,
    Guid? CafeteriaItemId = null,
    Guid? VariantId = null);

public record UpsertLoyaltyOfferRewardRequest(
    LoyaltyRewardMetric Metric,
    decimal Quantity,
    Guid? CafeteriaItemId = null,
    Guid? VariantId = null);

public record CreateLoyaltyOfferRequest(
    string Title,
    string? Description,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    LoyaltyPlayerScope PlayerScope,
    LoyaltyFulfillment Fulfillment,
    LoyaltyConditionLogic ConditionLogic,
    IReadOnlyList<UpsertLoyaltyOfferConditionRequest> Conditions,
    IReadOnlyList<UpsertLoyaltyOfferRewardRequest> Rewards,
    IReadOnlyList<Guid>? DeviceIds = null);

public record UpdateLoyaltyOfferRequest(
    string Title,
    string? Description,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    LoyaltyPlayerScope PlayerScope,
    LoyaltyFulfillment Fulfillment,
    LoyaltyConditionLogic ConditionLogic,
    IReadOnlyList<UpsertLoyaltyOfferConditionRequest> Conditions,
    IReadOnlyList<UpsertLoyaltyOfferRewardRequest> Rewards,
    IReadOnlyList<Guid>? DeviceIds = null);

public record LoyaltyCreditDto(
    Guid Id,
    Guid CustomerId,
    Guid OfferId,
    string OfferTitle,
    LoyaltyRewardMetric RewardMetric,
    decimal QuantityOriginal,
    decimal QuantityRemaining,
    Guid? CafeteriaItemId,
    string? CafeteriaItemName,
    Guid? VariantId,
    string? VariantName,
    LoyaltyCreditStatus Status,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record RedeemLoyaltyCreditRequest(
    Guid CreditId,
    Guid SessionId,
    decimal Quantity);

public interface ILoyaltyOfferService
{
    Task<IReadOnlyList<LoyaltyOfferDto>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task<LoyaltyOfferDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoyaltyOfferDto> CreateAsync(CreateLoyaltyOfferRequest request, CancellationToken ct = default);
    Task<LoyaltyOfferDto> UpdateAsync(Guid id, UpdateLoyaltyOfferRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<LoyaltyCreditDto>> GetCustomerCreditsAsync(Guid customerId, bool availableOnly = true, CancellationToken ct = default);
    Task RedeemCreditAsync(RedeemLoyaltyCreditRequest request, CancellationToken ct = default);

    /// <summary>Evaluate active offers against a just-closed session and apply rewards / bank credits.</summary>
    Task EvaluateAfterSessionCloseAsync(Guid sessionId, CancellationToken ct = default);
}
