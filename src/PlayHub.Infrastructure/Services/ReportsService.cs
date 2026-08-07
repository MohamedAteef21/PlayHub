using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Reports;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public ReportsService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(
        DateTime from, DateTime to, Guid? branchId = null, CancellationToken ct = default)
    {
        EnsureMaster();

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1).AddTicks(-1);
        var effectiveBranchId = await ResolveReportBranchIdAsync(branchId, ct);

        var entries = await _db.RevenueEntries
            .Where(r => r.BranchId == effectiveBranchId
                && r.RecordedAt >= fromDate && r.RecordedAt <= toDate)
            .ToListAsync(ct);
        var sessionRev = entries.Where(e => e.RevenueType == RevenueType.Session).Sum(e => e.Amount);
        var cafeteriaRev = entries.Where(e => e.RevenueType == RevenueType.Cafeteria).Sum(e => e.Amount);

        var daily = new List<DailyRevenueDto>();
        for (var day = DateOnly.FromDateTime(fromDate); day <= DateOnly.FromDateTime(toDate); day = day.AddDays(1))
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var dayEntries = entries.Where(e => e.RecordedAt >= dayStart && e.RecordedAt <= dayEnd).ToList();
            var s = dayEntries.Where(e => e.RevenueType == RevenueType.Session).Sum(e => e.Amount);
            var c = dayEntries.Where(e => e.RevenueType == RevenueType.Cafeteria).Sum(e => e.Amount);
            daily.Add(new DailyRevenueDto(day, s, c, s + c));
        }

        return new RevenueReportDto(fromDate, toDate, effectiveBranchId, sessionRev + cafeteriaRev, sessionRev, cafeteriaRev, daily);
    }

    public async Task<IReadOnlyList<BestSellerDto>> GetBestSellersAsync(
        DateTime from, DateTime to, int top = 10, CancellationToken ct = default)
    {
        EnsureMaster();
        var branchId = await ResolveReportBranchIdAsync(null, ct);

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1).AddTicks(-1);

        var saleLines = _db.CafeteriaSaleLines
            .Include(l => l.Sale)
            .Include(l => l.CafeteriaItem)
            .Where(l => l.Sale.BranchId == branchId
                && l.Sale.SoldAt >= fromDate && l.Sale.SoldAt <= toDate);

        var sessionLines = _db.SessionCafeteriaLines
            .Include(l => l.Session)
            .Include(l => l.CafeteriaItem)
            .Where(l => l.Session.BranchId == branchId
                && l.Session.ClosedAt >= fromDate && l.Session.ClosedAt <= toDate);

        var standalone = await saleLines
            .GroupBy(l => new { l.CafeteriaItemId, l.CafeteriaItem.Name })
            .Select(g => new { g.Key.CafeteriaItemId, g.Key.Name, Qty = g.Sum(x => x.Quantity - x.ReturnedQuantity), Rev = g.Sum(x => x.LineTotal) })
            .ToListAsync(ct);

        var session = await sessionLines
            .GroupBy(l => new { l.CafeteriaItemId, l.CafeteriaItem.Name })
            .Select(g => new { g.Key.CafeteriaItemId, g.Key.Name, Qty = g.Sum(x => x.Quantity), Rev = g.Sum(x => x.LineTotal) })
            .ToListAsync(ct);

        return standalone.Concat(session)
            .GroupBy(x => new { x.CafeteriaItemId, x.Name })
            .Select(g => new BestSellerDto(g.Key.CafeteriaItemId, g.Key.Name, g.Sum(x => x.Qty), g.Sum(x => x.Rev)))
            .OrderByDescending(x => x.TotalQuantity)
            .Take(top)
            .ToList();
    }

    public async Task<IReadOnlyList<DeviceUsageDto>> GetDeviceUsageAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        EnsureMaster();
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1).AddTicks(-1);

        var sessions = await _db.Sessions
            .Include(s => s.Device).ThenInclude(d => d.Room)
            .Where(s => s.BranchId == branchId && s.Status == SessionStatus.Closed &&
                s.ClosedAt >= fromDate && s.ClosedAt <= toDate)
            .ToListAsync(ct);

        return sessions
            .GroupBy(s => s.DeviceId)
            .Select(g =>
            {
                var device = g.First().Device;
                var totalSeconds = g.Sum(s =>
                {
                    if (!s.ClosedAt.HasValue) return 0;
                    return Math.Max(0, (int)(s.ClosedAt.Value - s.StartedAt).TotalSeconds - s.TotalPausedSeconds);
                });
                return new DeviceUsageDto(
                    device.Id,
                    device.Identifier,
                    device.Name,
                    device.Room?.Name ?? "—",
                    Math.Round(totalSeconds / 3600.0, 2),
                    g.Count());
            })
            .OrderByDescending(d => d.TotalHours)
            .ToList();
    }

    public async Task<CashDrawerDto> GetCashDrawerAsync(
        DateOnly date, int tzOffsetMinutes, Guid? branchId = null, CancellationToken ct = default)
    {
        EnsureMaster();
        var effectiveBranchId = await ResolveReportBranchIdAsync(branchId, ct);

        // Local business day converted to the UTC window it covers.
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-tzOffsetMinutes);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var paymentsQuery = _db.InvoicePayments
            .Where(p => !p.Invoice.IsDeleted
                && p.Invoice.BranchId == effectiveBranchId
                && p.Invoice.ClosedAt >= dayStartUtc && p.Invoice.ClosedAt < dayEndUtc);

        var payments = await paymentsQuery
            .Select(p => new { p.PaymentMethod, p.Amount, p.Status, p.Invoice.InvoiceType })
            .ToListAsync(ct);

        var cashSessions = payments
            .Where(p => p.PaymentMethod == PaymentMethod.Cash && p.Status == PaymentStatus.Completed && p.InvoiceType == InvoiceType.Session)
            .Sum(p => p.Amount);
        var cashCafeteria = payments
            .Where(p => p.PaymentMethod == PaymentMethod.Cash && p.Status == PaymentStatus.Completed && p.InvoiceType == InvoiceType.Cafeteria)
            .Sum(p => p.Amount);
        var bankTransferIn = payments
            .Where(p => p.PaymentMethod == PaymentMethod.BankTransfer && p.Status != PaymentStatus.Deferred)
            .Sum(p => p.Amount);
        var digitalWalletIn = payments
            .Where(p => p.PaymentMethod == PaymentMethod.DigitalWallet && p.Status != PaymentStatus.Deferred)
            .Sum(p => p.Amount);
        var paidFromCustomerWallets = payments
            .Where(p => p.PaymentMethod == PaymentMethod.CustomerWallet)
            .Sum(p => p.Amount);
        var newDeferredDebts = payments
            .Where(p => p.Status == PaymentStatus.Deferred)
            .Sum(p => p.Amount);

        // Old debts collected in cash during this day (regardless of when the invoice was closed).
        var cashCollectedDebts = await _db.InvoicePayments
            .Where(p => !p.Invoice.IsDeleted
                && p.Invoice.BranchId == effectiveBranchId
                && p.Status == PaymentStatus.Collected
                && p.CollectionMethod == PaymentMethod.Cash
                && p.CollectedAt >= dayStartUtc && p.CollectedAt < dayEndUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // Wallet top-ups are handed over in cash at the counter (bonus credit is excluded — no cash moves).
        var cashWalletTopUps = await _db.WalletTransactions
            .Where(w => w.Type == WalletTransactionType.TopUp
                && w.CreatedAt >= dayStartUtc && w.CreatedAt < dayEndUtc
                && (w.BranchId == effectiveBranchId || w.BranchId == null))
            .SumAsync(w => (decimal?)w.Amount, ct) ?? 0m;

        var dayCashbox = await _db.Expenses
            .Where(e => e.ExpenseDate == date && e.BranchId == effectiveBranchId)
            .Select(e => new { e.Amount, e.Category.Kind })
            .ToListAsync(ct);
        var cashExpenses = dayCashbox.Where(e => e.Kind == ExpenseCategoryKind.Expense).Sum(e => e.Amount);
        var cashManualIn = dayCashbox.Where(e => e.Kind == ExpenseCategoryKind.Revenue).Sum(e => e.Amount);

        var dayCollections = await _db.CashCollections
            .Where(c => c.BranchId == effectiveBranchId
                && c.CollectedAt >= dayStartUtc && c.CollectedAt < dayEndUtc)
            .OrderByDescending(c => c.CollectedAt)
            .Select(c => new CashCollectionDto(
                c.Id, c.Amount, c.Note,
                (c.CollectedByUser.FirstName + " " + c.CollectedByUser.LastName).Trim(),
                c.CollectedAt))
            .ToListAsync(ct);

        var collectedOnDay = dayCollections.Sum(c => c.Amount);
        var totalCashIn = cashSessions + cashCafeteria + cashWalletTopUps + cashCollectedDebts + cashManualIn;
        var drawerBalance = await ComputeDrawerBalanceAsync(effectiveBranchId, ct);

        return new CashDrawerDto(
            date,
            effectiveBranchId,
            cashSessions,
            cashCafeteria,
            cashWalletTopUps,
            cashCollectedDebts,
            cashManualIn,
            totalCashIn,
            cashExpenses,
            totalCashIn - cashExpenses,
            bankTransferIn,
            digitalWalletIn,
            paidFromCustomerWallets,
            newDeferredDebts,
            collectedOnDay,
            drawerBalance,
            dayCollections);
    }

    public async Task<CashDrawerDto> CollectCashAsync(CollectCashRequest request, CancellationToken ct = default)
    {
        EnsureMaster();
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var amount = decimal.Round(request.Amount, 2);
        if (amount <= 0)
            throw new InvalidOperationException("Collection amount must be greater than zero.");

        var balance = await ComputeDrawerBalanceAsync(branchId, ct);
        if (amount > balance)
            throw new InvalidOperationException($"Cannot collect more than the drawer balance ({balance:0.##}).");

        // Treat "collect all" as exact balance to avoid leftover cents from rounding.
        if (balance - amount < 0.01m)
            amount = balance;

        _db.CashCollections.Add(new CashCollection
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Amount = amount,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CollectedByUserId = _tenantContext.UserId,
            CollectedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CashDrawer.Collected", "CashCollection", null, new { Amount = amount, request.Note }, ct: ct);

        // Fresh read so the client gets the post-collection drawer balance (0 after collect-all).
        return await GetCashDrawerAsync(request.Date, request.TzOffsetMinutes, branchId, ct);
    }

    /// <summary>Running till balance: all cash ever received minus expenses and master collections.</summary>
    private async Task<decimal> ComputeDrawerBalanceAsync(Guid branchId, CancellationToken ct)
    {
        var cashIn = await _db.InvoicePayments.Where(p => !p.Invoice.IsDeleted
            && p.Invoice.BranchId == branchId
            && ((p.PaymentMethod == PaymentMethod.Cash && p.Status == PaymentStatus.Completed)
                || (p.Status == PaymentStatus.Collected && p.CollectionMethod == PaymentMethod.Cash)))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var topUps = await _db.WalletTransactions
            .Where(w => w.Type == WalletTransactionType.TopUp
                && (w.BranchId == branchId || w.BranchId == null))
            .SumAsync(w => (decimal?)w.Amount, ct) ?? 0m;

        var cashbox = await _db.Expenses
            .Where(e => e.BranchId == branchId)
            .Select(e => new { e.Amount, e.Category.Kind })
            .ToListAsync(ct);
        var expenses = cashbox.Where(e => e.Kind == ExpenseCategoryKind.Expense).Sum(e => e.Amount);
        var manualIn = cashbox.Where(e => e.Kind == ExpenseCategoryKind.Revenue).Sum(e => e.Amount);

        var collected = await _db.CashCollections
            .Where(c => c.BranchId == branchId)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return decimal.Round(cashIn + topUps + manualIn - expenses - collected, 2);
    }

    /// <summary>
    /// Masters must always report against an owned branch — never tenant-wide aggregates.
    /// </summary>
    private async Task<Guid> ResolveReportBranchIdAsync(Guid? requestedBranchId, CancellationToken ct)
    {
        if (requestedBranchId is Guid requested)
        {
            if (!_tenantContext.IsSuperAdmin && !_tenantContext.AllowedBranchIds.Contains(requested))
                throw new UnauthorizedAccessException("You do not have access to this branch.");

            if (!_tenantContext.IsSuperAdmin)
            {
                var businessOwnerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
                var branchOwnerId = await _db.Branches.AsNoTracking()
                    .Where(b => b.Id == requested)
                    .Select(b => b.OwnerUserId)
                    .FirstOrDefaultAsync(ct);
                if (branchOwnerId.HasValue && branchOwnerId.Value != businessOwnerId)
                    throw new UnauthorizedAccessException("You do not have access to this branch.");
            }

            return requested;
        }

        return await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
    }

    private void EnsureMaster()
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Reports are visible to the master user only.");
    }
}
