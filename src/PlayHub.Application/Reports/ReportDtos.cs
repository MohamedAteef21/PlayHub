namespace PlayHub.Application.Reports;

public record BestSellerDto(Guid ItemId, string ItemName, int TotalQuantity, decimal TotalRevenue);

public record DeviceUsageDto(
    Guid DeviceId,
    string DeviceIdentifier,
    string DeviceName,
    string RoomName,
    double TotalHours,
    int SessionCount);

public record RevenueReportDto(
    DateTime From,
    DateTime To,
    Guid? BranchId,
    decimal TotalRevenue,
    decimal SessionRevenue,
    decimal CafeteriaRevenue,
    IReadOnlyList<DailyRevenueDto> Daily);

public record DailyRevenueDto(DateOnly Date, decimal SessionRevenue, decimal CafeteriaRevenue, decimal Total);

/// <summary>Cash-drawer snapshot for one business day: what physically entered/left the till.</summary>
public record CashDrawerDto(
    DateOnly Date,
    Guid? BranchId,
    decimal CashSessions,
    decimal CashCafeteria,
    decimal CashWalletTopUps,
    decimal CashCollectedDebts,
    decimal TotalCashIn,
    decimal CashExpenses,
    decimal NetCash,
    decimal BankTransferIn,
    decimal DigitalWalletIn,
    decimal PaidFromCustomerWallets,
    decimal NewDeferredDebts,
    decimal CollectedOnDay,
    decimal DrawerBalance,
    IReadOnlyList<CashCollectionDto> DayCollections);

public record CashCollectionDto(
    Guid Id,
    decimal Amount,
    string? Note,
    string CollectedByName,
    DateTime CollectedAt);

public record CollectCashRequest(decimal Amount, string? Note, DateOnly Date, int TzOffsetMinutes);

public interface IReportsService
{
    Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to, Guid? branchId = null, CancellationToken ct = default);
    Task<IReadOnlyList<BestSellerDto>> GetBestSellersAsync(DateTime from, DateTime to, int top = 10, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceUsageDto>> GetDeviceUsageAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<CashDrawerDto> GetCashDrawerAsync(DateOnly date, int tzOffsetMinutes, Guid? branchId = null, CancellationToken ct = default);
    Task<CashDrawerDto> CollectCashAsync(CollectCashRequest request, CancellationToken ct = default);
}
