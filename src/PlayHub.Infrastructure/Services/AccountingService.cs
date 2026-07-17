using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Accounting;
using PlayHub.Application.Common;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AccountingService : IAccountingService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public AccountingService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var query = _db.ExpenseCategories.AsQueryable();
        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            query = query.Where(c => c.OwnerUserId == ownerId);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.NameAr, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default)
    {
        var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        var category = new ExpenseCategory
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
            Name = request.Name.Trim(),
            NameAr = request.NameAr?.Trim()
        };

        _db.ExpenseCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ExpenseCategory.Created", "ExpenseCategory", category.Id, new { category.Name }, ct: ct);

        return new ExpenseCategoryDto(category.Id, category.Name, category.NameAr, category.IsActive);
    }

    public async Task<ExpenseCategoryDto> UpdateCategoryAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken ct = default)
    {
        var category = await RequireOwnedCategoryAsync(id, ct);

        category.Name = request.Name.Trim();
        category.NameAr = request.NameAr?.Trim();
        category.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ExpenseCategory.Updated", "ExpenseCategory", category.Id, new { category.Name }, ct: ct);

        return new ExpenseCategoryDto(category.Id, category.Name, category.NameAr, category.IsActive);
    }

    public async Task SoftDeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await RequireOwnedCategoryAsync(id, ct);

        category.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        category.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ExpenseCategory.SoftDeleted", "ExpenseCategory", category.Id, new { category.Name }, ct: ct);
    }

    private async Task<ExpenseCategory> RequireOwnedCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Category not found.");

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(category.OwnerUserId, ownerId, false))
                throw new KeyNotFoundException("Category not found.");
        }

        return category;
    }

    public async Task<PagedResult<ExpenseDto>> GetExpensesAsync(
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Branch)
            .Include(e => e.RecordedByUser)
            .Where(e => e.BranchId == branchId);

        if (from.HasValue)
            query = query.Where(e => e.ExpenseDate >= DateOnly.FromDateTime(from.Value));
        if (to.HasValue)
            query = query.Where(e => e.ExpenseDate <= DateOnly.FromDateTime(to.Value));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<ExpenseDto>(items.Select(MapExpense).ToList(), total, p, size);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        if (request.Amount <= 0)
            throw new InvalidOperationException("Expense amount must be greater than zero.");

        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct)
            ?? throw new KeyNotFoundException("Expense category not found.");

        var expense = new Expense
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            CategoryId = category.Id,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            ExpenseDate = request.ExpenseDate,
            RecordedByUserId = _tenantContext.UserId
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Expense.Created", "Expense", expense.Id, new { expense.Amount, category.Name }, ct: ct);

        await _db.Entry(expense).Reference(e => e.Category).LoadAsync(ct);
        await _db.Entry(expense).Reference(e => e.Branch).LoadAsync(ct);
        await _db.Entry(expense).Reference(e => e.RecordedByUser).LoadAsync(ct);

        return MapExpense(expense);
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(Guid id, UpdateExpenseRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        if (request.Amount <= 0)
            throw new InvalidOperationException("Expense amount must be greater than zero.");

        var expense = await _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Branch)
            .Include(e => e.RecordedByUser)
            .FirstOrDefaultAsync(e => e.Id == id && e.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Expense not found.");

        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct)
            ?? throw new KeyNotFoundException("Expense category not found.");

        expense.CategoryId = category.Id;
        expense.Amount = request.Amount;
        expense.Description = request.Description.Trim();
        expense.ExpenseDate = request.ExpenseDate;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Expense.Updated", "Expense", expense.Id, new { expense.Amount, category.Name }, ct: ct);

        await _db.Entry(expense).Reference(e => e.Category).LoadAsync(ct);
        return MapExpense(expense);
    }

    public async Task SoftDeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Expense not found.");

        expense.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Expense.SoftDeleted", "Expense", expense.Id, new { expense.Amount, expense.Description }, ct: ct);
    }

    public async Task<FinancialDashboardDto> GetDashboardAsync(
        DateTime from, DateTime to, Guid? branchId = null, CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster && branchId.HasValue && branchId != _tenantContext.BranchId)
            throw new UnauthorizedAccessException("Cannot view other branch financials.");

        var effectiveBranchId = _tenantContext.IsMaster ? branchId : _tenantContext.BranchId;
        if (!_tenantContext.IsMaster)
            effectiveBranchId = BranchGuard.RequireBranchId(_tenantContext);

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1).AddTicks(-1);

        var revenueQuery = _db.RevenueEntries.AsQueryable();
        var expenseQuery = _db.Expenses.AsQueryable();

        if (effectiveBranchId.HasValue)
        {
            revenueQuery = revenueQuery.Where(r => r.BranchId == effectiveBranchId.Value);
            expenseQuery = expenseQuery.Where(e => e.BranchId == effectiveBranchId.Value);
        }

        revenueQuery = revenueQuery.Where(r => r.RecordedAt >= fromDate && r.RecordedAt <= toDate);
        expenseQuery = expenseQuery.Where(e =>
            e.ExpenseDate >= DateOnly.FromDateTime(fromDate) &&
            e.ExpenseDate <= DateOnly.FromDateTime(toDate));

        var totalRevenue = await revenueQuery.SumAsync(r => r.Amount, ct);
        var totalExpenses = await expenseQuery.SumAsync(e => e.Amount, ct);

        var branches = await _db.Branches.Where(b => b.IsActive).ToListAsync(ct);
        if (effectiveBranchId.HasValue)
            branches = branches.Where(b => b.Id == effectiveBranchId.Value).ToList();

        var byBranch = new List<BranchFinancialSummaryDto>();
        foreach (var branch in branches)
        {
            var rev = await _db.RevenueEntries
                .Where(r => r.BranchId == branch.Id && r.RecordedAt >= fromDate && r.RecordedAt <= toDate)
                .SumAsync(r => r.Amount, ct);
            var exp = await _db.Expenses
                .Where(e => e.BranchId == branch.Id &&
                    e.ExpenseDate >= DateOnly.FromDateTime(fromDate) &&
                    e.ExpenseDate <= DateOnly.FromDateTime(toDate))
                .SumAsync(e => e.Amount, ct);

            byBranch.Add(new BranchFinancialSummaryDto(branch.Id, branch.Name, rev, exp, rev - exp));
        }

        var expensesByCategory = (await expenseQuery
            .GroupBy(e => new { e.CategoryId, e.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Total = g.Sum(e => e.Amount) })
            .ToListAsync(ct))
            .OrderByDescending(c => c.Total)
            .Select(c => new CategoryExpenseSummaryDto(c.CategoryId, c.Name, c.Total))
            .ToList();

        var dailyBreakdown = new List<DailyFinancialDto>();
        for (var day = DateOnly.FromDateTime(fromDate); day <= DateOnly.FromDateTime(toDate); day = day.AddDays(1))
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var revQ = _db.RevenueEntries.Where(r => r.RecordedAt >= dayStart && r.RecordedAt <= dayEnd);
            var expQ = _db.Expenses.Where(e => e.ExpenseDate == day);

            if (effectiveBranchId.HasValue)
            {
                revQ = revQ.Where(r => r.BranchId == effectiveBranchId.Value);
                expQ = expQ.Where(e => e.BranchId == effectiveBranchId.Value);
            }

            var dayRev = await revQ.SumAsync(r => r.Amount, ct);
            var dayExp = await expQ.SumAsync(e => e.Amount, ct);
            dailyBreakdown.Add(new DailyFinancialDto(day, dayRev, dayExp, dayRev - dayExp));
        }

        return new FinancialDashboardDto(
            fromDate, toDate, effectiveBranchId,
            totalRevenue, totalExpenses, totalRevenue - totalExpenses,
            byBranch, expensesByCategory, dailyBreakdown);
    }

    private static ExpenseDto MapExpense(Expense e) =>
        new(e.Id, e.BranchId, e.Branch.Name, e.CategoryId, e.Category.Name,
            e.Amount, e.Description, e.ExpenseDate, e.PurchaseOrderId,
            e.RecordedByUser.FullName, e.CreatedAt);
}
