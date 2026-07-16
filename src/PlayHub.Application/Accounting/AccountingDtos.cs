namespace PlayHub.Application.Accounting;

public record ExpenseCategoryDto(Guid Id, string Name, string? NameAr, bool IsActive);

public record CreateExpenseCategoryRequest(string Name, string? NameAr);

public record UpdateExpenseCategoryRequest(string Name, string? NameAr, bool IsActive);

public record ExpenseDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    Guid CategoryId,
    string CategoryName,
    decimal Amount,
    string Description,
    DateOnly ExpenseDate,
    Guid? PurchaseOrderId,
    string RecordedByName,
    DateTime CreatedAt);

public record CreateExpenseRequest(
    Guid CategoryId,
    decimal Amount,
    string Description,
    DateOnly ExpenseDate);

public record FinancialDashboardDto(
    DateTime From,
    DateTime To,
    Guid? BranchId,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    IReadOnlyList<BranchFinancialSummaryDto> ByBranch,
    IReadOnlyList<CategoryExpenseSummaryDto> ExpensesByCategory,
    IReadOnlyList<DailyFinancialDto> DailyBreakdown);

public record BranchFinancialSummaryDto(
    Guid BranchId,
    string BranchName,
    decimal Revenue,
    decimal Expenses,
    decimal NetProfit);

public record CategoryExpenseSummaryDto(Guid CategoryId, string CategoryName, decimal Total);

public record DailyFinancialDto(DateOnly Date, decimal Revenue, decimal Expenses, decimal NetProfit);

public interface IAccountingService
{
    Task<IReadOnlyList<ExpenseCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ExpenseCategoryDto> CreateCategoryAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default);
    Task<ExpenseCategoryDto> UpdateCategoryAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken ct = default);

    Task<PlayHub.Application.Common.PagedResult<ExpenseDto>> GetExpensesAsync(
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default);

    Task<FinancialDashboardDto> GetDashboardAsync(DateTime from, DateTime to, Guid? branchId = null, CancellationToken ct = default);
}
