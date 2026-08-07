using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class BillingService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;

    public BillingService(PlayHubDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Invoice> CreateInvoiceAsync(
        Guid branchId,
        InvoiceType invoiceType,
        Guid? sessionId,
        Guid? cafeteriaSaleId,
        decimal total,
        PaymentRequest payment,
        RevenueType revenueType,
        CancellationToken ct)
    {
        PaymentValidator.Validate(payment);

        var branch = await _db.Branches.FirstAsync(b => b.Id == branchId, ct);
        var invoiceNumber = $"{branch.InvoicePrefix}-{branch.NextInvoiceNumber:D5}";
        branch.NextInvoiceNumber++;

        var invoiceStatus = payment.PaymentMethod == PaymentMethod.Deferred
            ? InvoiceStatus.Deferred
            : InvoiceStatus.Paid;

        var paymentStatus = payment.PaymentMethod switch
        {
            PaymentMethod.Cash => PaymentStatus.Completed,
            PaymentMethod.Deferred => PaymentStatus.Deferred,
            PaymentMethod.BankTransfer or PaymentMethod.DigitalWallet => string.IsNullOrWhiteSpace(payment.ProofFileUrl)
                ? PaymentStatus.PendingVerification
                : PaymentStatus.Completed,
            _ => PaymentStatus.Completed
        };

        var closedAt = DateTime.UtcNow;
        var invoice = new Invoice
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            InvoiceNumber = invoiceNumber,
            InvoiceType = invoiceType,
            SessionId = sessionId,
            CafeteriaSaleId = cafeteriaSaleId,
            Subtotal = total,
            Total = total,
            Status = invoiceStatus,
            ClosedByUserId = _tenantContext.UserId,
            ClosedAt = closedAt
        };

        var invoicePayment = new InvoicePayment
        {
            PaymentMethod = payment.PaymentMethod,
            Amount = total,
            Status = paymentStatus,
            DebtorName = payment.DebtorName?.Trim(),
            DebtorPhone = payment.DebtorPhone?.Trim(),
            CustomerId = payment.CustomerId
        };

        if (!string.IsNullOrWhiteSpace(payment.ProofFileUrl))
        {
            invoicePayment.Proof = new PaymentProof
            {
                FileUrl = payment.ProofFileUrl.Trim(),
                FileName = "receipt",
                ContentType = "image/jpeg",
                UploadedByUserId = _tenantContext.UserId
            };
        }

        invoice.Payments.Add(invoicePayment);
        invoice.RevenueEntry = new RevenueEntry
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Amount = total,
            RevenueType = revenueType,
            RecordedAt = closedAt
        };

        _db.Invoices.Add(invoice);
        return invoice;
    }

    public async Task CreateRevenueReversalAsync(
        Guid branchId,
        decimal refundAmount,
        RevenueType revenueType,
        CancellationToken ct)
    {
        var branch = await _db.Branches.FirstAsync(b => b.Id == branchId, ct);
        var invoiceNumber = $"{branch.InvoicePrefix}-CR-{branch.NextInvoiceNumber:D5}";
        branch.NextInvoiceNumber++;

        var amount = -Math.Abs(refundAmount);
        var closedAt = DateTime.UtcNow;

        var creditInvoice = new Invoice
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            InvoiceNumber = invoiceNumber,
            InvoiceType = InvoiceType.Cafeteria,
            Subtotal = amount,
            Total = amount,
            Status = InvoiceStatus.Paid,
            ClosedByUserId = _tenantContext.UserId,
            ClosedAt = closedAt
        };

        creditInvoice.RevenueEntry = new RevenueEntry
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Amount = amount,
            RevenueType = revenueType,
            RecordedAt = closedAt
        };

        _db.Invoices.Add(creditInvoice);
    }
}
