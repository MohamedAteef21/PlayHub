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
        var effectiveBranchId = branchId ?? _tenantContext.BranchId;

        var query = _db.RevenueEntries
            .Where(r => r.RecordedAt >= fromDate && r.RecordedAt <= toDate);

        if (effectiveBranchId.HasValue)
            query = query.Where(r => r.BranchId == effectiveBranchId.Value);

        var entries = await query.ToListAsync(ct);
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
        var branchId = _tenantContext.BranchId;

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1).AddTicks(-1);

        var saleLines = _db.CafeteriaSaleLines
            .Include(l => l.Sale)
            .Include(l => l.CafeteriaItem)
            .Where(l => l.Sale.SoldAt >= fromDate && l.Sale.SoldAt <= toDate);

        if (branchId.HasValue)
            saleLines = saleLines.Where(l => l.Sale.BranchId == branchId.Value);

        var sessionLines = _db.SessionCafeteriaLines
            .Include(l => l.Session)
            .Include(l => l.CafeteriaItem)
            .Where(l => l.Session.ClosedAt >= fromDate && l.Session.ClosedAt <= toDate);

        if (branchId.HasValue)
            sessionLines = sessionLines.Where(l => l.Session.BranchId == branchId.Value);

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
        var effectiveBranchId = branchId ?? _tenantContext.BranchId;

        // Local business day converted to the UTC window it covers.
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-tzOffsetMinutes);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var paymentsQuery = _db.InvoicePayments
            .Where(p => !p.Invoice.IsDeleted
                && p.Invoice.ClosedAt >= dayStartUtc && p.Invoice.ClosedAt < dayEndUtc);

        if (effectiveBranchId.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.Invoice.BranchId == effectiveBranchId.Value);

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
        var collectedQuery = _db.InvoicePayments
            .Where(p => !p.Invoice.IsDeleted
                && p.Status == PaymentStatus.Collected
                && p.CollectionMethod == PaymentMethod.Cash
                && p.CollectedAt >= dayStartUtc && p.CollectedAt < dayEndUtc);
        if (effectiveBranchId.HasValue)
            collectedQuery = collectedQuery.Where(p => p.Invoice.BranchId == effectiveBranchId.Value);
        var cashCollectedDebts = await collectedQuery.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // Wallet top-ups are handed over in cash at the counter (bonus credit is excluded — no cash moves).
        var topUpsDayQuery = _db.WalletTransactions
            .Where(w => w.Type == WalletTransactionType.TopUp
                && w.CreatedAt >= dayStartUtc && w.CreatedAt < dayEndUtc);
        if (effectiveBranchId.HasValue)
            topUpsDayQuery = topUpsDayQuery.Where(w => w.BranchId == effectiveBranchId.Value || w.BranchId == null);
        var cashWalletTopUps = await topUpsDayQuery.SumAsync(w => (decimal?)w.Amount, ct) ?? 0m;

        var expensesQuery = _db.Expenses.Where(e => e.ExpenseDate == date);
        if (effectiveBranchId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.BranchId == effectiveBranchId.Value);
        var cashExpenses = await expensesQuery.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var dayCollectionsQuery = _db.CashCollections
            .Where(c => c.CollectedAt >= dayStartUtc && c.CollectedAt < dayEndUtc);
        if (effectiveBranchId.HasValue)
            dayCollectionsQuery = dayCollectionsQuery.Where(c => c.BranchId == effectiveBranchId.Value);

        var dayCollections = await dayCollectionsQuery
            .OrderByDescending(c => c.CollectedAt)
            .Select(c => new CashCollectionDto(
                c.Id, c.Amount, c.Note,
                (c.CollectedByUser.FirstName + " " + c.CollectedByUser.LastName).Trim(),
                c.CollectedAt))
            .ToListAsync(ct);

        var collectedOnDay = dayCollections.Sum(c => c.Amount);
        var totalCashIn = cashSessions + cashCafeteria + cashWalletTopUps + cashCollectedDebts;
        var drawerBalance = await ComputeDrawerBalanceAsync(effectiveBranchId, ct);

        return new CashDrawerDto(
            date,
            effectiveBranchId,
            cashSessions,
            cashCafeteria,
            cashWalletTopUps,
            cashCollectedDebts,
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

        return await GetCashDrawerAsync(request.Date, request.TzOffsetMinutes, branchId, ct);
    }

    /// <summary>Running till balance: all cash ever received minus expenses and master collections.</summary>
    private async Task<decimal> ComputeDrawerBalanceAsync(Guid? branchId, CancellationToken ct)
    {
        var cashPaymentsQuery = _db.InvoicePayments.Where(p => !p.Invoice.IsDeleted
            && ((p.PaymentMethod == PaymentMethod.Cash && p.Status == PaymentStatus.Completed)
                || (p.Status == PaymentStatus.Collected && p.CollectionMethod == PaymentMethod.Cash)));
        if (branchId.HasValue)
            cashPaymentsQuery = cashPaymentsQuery.Where(p => p.Invoice.BranchId == branchId.Value);
        var cashIn = await cashPaymentsQuery.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var topUpsQuery = _db.WalletTransactions.Where(w => w.Type == WalletTransactionType.TopUp);
        if (branchId.HasValue)
            topUpsQuery = topUpsQuery.Where(w => w.BranchId == branchId.Value || w.BranchId == null);
        var topUps = await topUpsQuery.SumAsync(w => (decimal?)w.Amount, ct) ?? 0m;

        var expensesQuery = _db.Expenses.AsQueryable();
        if (branchId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.BranchId == branchId.Value);
        var expenses = await expensesQuery.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var collectionsQuery = _db.CashCollections.AsQueryable();
        if (branchId.HasValue)
            collectionsQuery = collectionsQuery.Where(c => c.BranchId == branchId.Value);
        var collected = await collectionsQuery.SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return cashIn + topUps - expenses - collected;
    }

    private void EnsureMaster()
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Reports are visible to the master user only.");
    }
}
