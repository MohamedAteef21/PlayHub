using PlayHub.Domain.Enums;

namespace PlayHub.Application.Sessions;

public record SessionLiveDto(
    Guid Id,
    Guid BranchId,
    Guid DeviceId,
    string DeviceName,
    string DeviceIdentifier,
    Guid? RoomId,
    string? RoomName,
    SessionMode SessionMode,
    SessionStatus Status,
    Guid PricingPlanId,
    string PricingPlanName,
    int? ControllerCount,
    int? WatcherCount,
    DateTime StartedAt,
    DateTime? OriginalStartedAt,
    int TotalPausedSeconds,
    DateTime? PausedAt,
    int ElapsedSeconds,
    decimal AccruedTimeCost,
    decimal CurrentTimeCost,
    decimal RoomSurchargeCost,
    decimal CafeteriaCost,
    decimal TotalCost,
    string OpenedByName,
    int? PlannedDurationMinutes,
    int? RemainingSeconds,
    bool TimeExpired,
    bool CanConvertToGaming,
    Guid? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    string? CustomerPhone,
    bool IsQuickGuest,
    string? QuickGuestName,
    IReadOnlyList<SessionCafeteriaLineDto> CafeteriaLines);

public record OpenSessionRequest(
    Guid DeviceId,
    Guid PricingPlanId,
    SessionMode SessionMode,
    int? ControllerCount,
    int? WatcherCount,
    /// <summary>Fixed booking length in minutes. Null = open-ended timer.</summary>
    int? PlannedDurationMinutes = null,
    Guid? CustomerId = null,
    string? QuickGuestName = null,
    bool IsQuickGuest = false);

/// <summary>
/// Convert an open Watching session to hourly Gaming.
/// Accrues watching cost, then starts a new gaming timer with individual (1) or couple (2) pricing.
/// </summary>
public record ConvertSessionRequest(
    Guid PricingPlanId,
    /// <summary>1 = individual (فردي), 2 = couple (زوجي).</summary>
    int ControllerCount);

public record CloseSessionPaymentRequest(
    PaymentMethod PaymentMethod,
    string? DebtorName,
    string? DebtorPhone,
    string? ProofFileUrl,
    /// <summary>Part of the bill paid from the customer's prepaid wallet (split payment). 0 = none.</summary>
    decimal WalletAmount = 0);

public record CloseSessionRequest(
    CloseSessionPaymentRequest Payment,
    decimal DiscountAmount = 0,
    string? DiscountReason = null);

public record AddSessionCafeteriaRequest(
    Guid CafeteriaItemId,
    Guid VariantId,
    int Quantity,
    /// <summary>How much stock to deduct from the parent product.</summary>
    int StockDeductQuantity,
    string? CustomerName = null,
    InventoryUnitKind Unit = InventoryUnitKind.Base);

/// <summary>AdditionalMinutes = null switches the session to an open (unlimited) timer.</summary>
public record ExtendSessionRequest(int? AdditionalMinutes);

public record UpdateWatchersRequest(int WatcherCount);

public record ReturnSessionCafeteriaRequest(Guid SessionCafeteriaLineId, int Quantity, string Reason);

public record SessionCafeteriaReturnDto(
    Guid Id,
    Guid SessionId,
    Guid SessionCafeteriaLineId,
    int Quantity,
    string Reason,
    decimal RefundAmount,
    DateTime ReturnedAt);

public record SessionDetailDto(
    Guid Id,
    Guid BranchId,
    Guid DeviceId,
    string DeviceName,
    Guid? RoomId,
    string? RoomName,
    SessionMode SessionMode,
    SessionStatus Status,
    Guid PricingPlanId,
    string PricingPlanName,
    int? ControllerCount,
    int? WatcherCount,
    DateTime StartedAt,
    DateTime? OriginalStartedAt,
    DateTime? ClosedAt,
    int TotalPausedSeconds,
    decimal AccruedTimeCost,
    decimal TimeCost,
    decimal RoomSurchargeCost,
    decimal CafeteriaCost,
    decimal DiscountAmount,
    string? DiscountReason,
    decimal TotalCost,
    string OpenedByName,
    string? ClosedByName,
    int? PlannedDurationMinutes,
    Guid? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    string? CustomerPhone,
    bool IsQuickGuest,
    string? QuickGuestName,
    string? InvoiceNumber,
    IReadOnlyList<SessionCafeteriaLineDto> CafeteriaLines,
    SessionInvoiceDto? Invoice);

public record SessionCafeteriaLineDto(
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
    string? CustomerName,
    DateTime AddedAt);

public record SessionInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    decimal Total,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus);

/// <summary>Past and current sessions for history view (newest first).</summary>
public record SessionHistoryDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string? RoomName,
    SessionMode SessionMode,
    SessionStatus Status,
    DateTime StartedAt,
    DateTime? ClosedAt,
    string OpenedByName,
    string? ClosedByName,
    decimal TimeCost,
    decimal CafeteriaCost,
    decimal TotalCost,
    Guid? CustomerId,
    string? CustomerName,
    bool IsQuickGuest,
    string? QuickGuestName);

public interface ISessionService
{
    Task<IReadOnlyList<SessionLiveDto>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task<PlayHub.Application.Common.PagedResult<SessionHistoryDto>> GetSessionHistoryAsync(
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<SessionDetailDto?> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<SessionLiveDto> OpenSessionAsync(OpenSessionRequest request, CancellationToken ct = default);
    Task<SessionLiveDto> PauseSessionAsync(Guid id, CancellationToken ct = default);
    Task<SessionLiveDto> ResumeSessionAsync(Guid id, CancellationToken ct = default);
    Task<SessionLiveDto> ExtendSessionAsync(Guid id, ExtendSessionRequest request, CancellationToken ct = default);
    Task<SessionLiveDto> UpdateWatcherCountAsync(Guid id, UpdateWatchersRequest request, CancellationToken ct = default);
    Task<SessionLiveDto> ConvertSessionAsync(Guid id, ConvertSessionRequest request, CancellationToken ct = default);
    Task<SessionDetailDto> CloseSessionAsync(Guid id, CloseSessionRequest request, CancellationToken ct = default);
    Task<SessionLiveDto> AddCafeteriaItemAsync(Guid id, AddSessionCafeteriaRequest request, CancellationToken ct = default);
    Task<SessionLiveDto> ReturnCafeteriaItemAsync(Guid id, ReturnSessionCafeteriaRequest request, CancellationToken ct = default);
}

public interface ISessionNotifier
{
    Task NotifySessionUpdatedAsync(Guid branchId, SessionLiveDto session, CancellationToken ct = default);
    Task NotifySessionClosedAsync(Guid branchId, Guid sessionId, CancellationToken ct = default);
}

public interface ISessionCostCalculator
{
    int GetElapsedSeconds(SessionStatus status, DateTime startedAt, int totalPausedSeconds, DateTime? activePauseStartedAt, DateTime? closedAt = null);
    decimal CalculateTimeCost(string rateSnapshotJson, SessionMode mode, int elapsedSeconds, int? controllerCount, int? watcherCount, bool billingRoundUp);
}
