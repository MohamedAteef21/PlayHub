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

    public async Task<IReadOnlyList<ReceivableDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Receivables are visible to the master user only.");

        var query = _db.InvoicePayments
            .Include(p => p.Invoice).ThenInclude(i => i.Branch)
            .Where(p => p.Status == PaymentStatus.Deferred && p.PaymentMethod == PaymentMethod.Deferred);

        if (_tenantContext.BranchId.HasValue)
            query = query.Where(p => p.Invoice.BranchId == _tenantContext.BranchId.Value);

        var payments = await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);

        return payments.Select(p => new ReceivableDto(
            p.Id,
            p.InvoiceId,
            p.Invoice.InvoiceNumber,
            p.Amount,
            p.DebtorName ?? "Unknown",
            p.DebtorPhone,
            p.CreatedAt,
            (int)(DateTime.UtcNow - p.CreatedAt).TotalDays,
            p.Invoice.BranchId,
            p.Invoice.Branch.Name,
            p.Invoice.InvoiceType)).ToList();
    }

    public async Task<ReceivableDto> CollectAsync(Guid paymentId, CollectReceivableRequest request, CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Only the master user can collect receivables.");

        if (request.CollectionMethod is PaymentMethod.BankTransfer or PaymentMethod.DigitalWallet)
        {
            // Receipt image is optional for bank/wallet collection.
        }

        var payment = await _db.InvoicePayments
            .Include(p => p.Invoice).ThenInclude(i => i.Branch)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Status == PaymentStatus.Deferred, ct)
            ?? throw new KeyNotFoundException("Deferred payment not found.");

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

        return new ReceivableDto(
            payment.Id,
            payment.InvoiceId,
            payment.Invoice.InvoiceNumber,
            payment.Amount,
            payment.DebtorName ?? "Unknown",
            payment.DebtorPhone,
            payment.CreatedAt,
            0,
            payment.Invoice.BranchId,
            payment.Invoice.Branch.Name,
            payment.Invoice.InvoiceType);
    }
}
