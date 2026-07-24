using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Receivables;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class ReceivableService : IReceivableService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public ReceivableService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ReceivableDto>> GetAllAsync(Guid? customerId = null, CancellationToken ct = default)
    {
        EnsureCanView();

        var query = BaseDeferredQuery();
        if (customerId.HasValue)
        {
            var customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId.Value, ct);
            if (customer is null)
                return [];

            query = query.Where(p =>
                p.CustomerId == customer.Id
                || (p.DebtorPhone != null && p.DebtorPhone == customer.Phone));
        }

        var payments = await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);
        return payments.Select(Map).ToList();
    }

    public async Task<ReceivableSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        EnsureCanView();

        var query = BaseDeferredQuery();
        var total = await query.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var count = await query.CountAsync(ct);
        return new ReceivableSummaryDto(total, count);
    }

    public async Task<ReceivableDto> CollectAsync(Guid paymentId, CollectReceivableRequest request, CancellationToken ct = default)
    {
        EnsureCanCollect();

        if (request.CollectionMethod is PaymentMethod.BankTransfer or PaymentMethod.DigitalWallet)
        {
            // Receipt image is optional for bank/wallet collection.
        }

        var payment = await _db.InvoicePayments
            .Include(p => p.Invoice).ThenInclude(i => i.Branch)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Status == PaymentStatus.Deferred, ct)
            ?? throw new KeyNotFoundException("Deferred payment not found.");

        if (!_tenantContext.IsMaster && _tenantContext.BranchId.HasValue
            && payment.Invoice.BranchId != _tenantContext.BranchId.Value)
            throw new UnauthorizedAccessException("Cannot collect receivables from another branch.");

        payment.Status = PaymentStatus.Collected;
        payment.CollectionMethod = request.CollectionMethod;
        payment.CollectedAt = DateTime.UtcNow;
        payment.CollectedByUserId = _tenantContext.UserId;
        payment.Invoice.Status = InvoiceStatus.Paid;

        if (!string.IsNullOrWhiteSpace(request.ProofFileUrl))
        {
            payment.Proof = new Domain.Entities.PaymentProof
            {
                FileUrl = request.ProofFileUrl.Trim(),
                FileName = "collection-receipt",
                ContentType = "image/jpeg",
                UploadedByUserId = _tenantContext.UserId
            };
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Receivable.Collected", "InvoicePayment", payment.Id, new
        {
            payment.Amount,
            payment.DebtorName,
            request.CollectionMethod
        }, ct: ct);

        return Map(payment);
    }

    private IQueryable<Domain.Entities.InvoicePayment> BaseDeferredQuery()
    {
        var query = _db.InvoicePayments
            .Include(p => p.Invoice).ThenInclude(i => i.Branch)
            .Where(p => p.Status == PaymentStatus.Deferred && p.PaymentMethod == PaymentMethod.Deferred);

        if (!_tenantContext.IsMaster && _tenantContext.BranchId.HasValue)
            query = query.Where(p => p.Invoice.BranchId == _tenantContext.BranchId.Value);

        return query;
    }

    private void EnsureCanView()
    {
        if (_tenantContext.IsMaster || _tenantContext.IsSuperAdmin)
            return;
        if (_tenantContext.Permissions.Contains("Customers.View")
            || _tenantContext.Permissions.Contains("Customers.Manage")
            || _tenantContext.Permissions.Contains("Reports.View"))
            return;
        throw new UnauthorizedAccessException("Receivables require customers or reports access.");
    }

    private void EnsureCanCollect()
    {
        if (_tenantContext.IsMaster || _tenantContext.IsSuperAdmin)
            return;
        if (_tenantContext.Permissions.Contains("Customers.Manage"))
            return;
        throw new UnauthorizedAccessException("Only users who can manage customers can collect receivables.");
    }

    private static ReceivableDto Map(Domain.Entities.InvoicePayment p) =>
        new(
            p.Id,
            p.InvoiceId,
            p.Invoice.InvoiceNumber,
            p.Amount,
            p.DebtorName ?? "Unknown",
            p.DebtorPhone,
            p.CustomerId,
            p.CreatedAt,
            (int)(DateTime.UtcNow - p.CreatedAt).TotalDays,
            p.Invoice.BranchId,
            p.Invoice.Branch.Name,
            p.Invoice.InvoiceType);
}
