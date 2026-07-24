using PlayHub.Domain.Enums;

namespace PlayHub.Application.Receivables;

public record ReceivableDto(
    Guid PaymentId,
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string DebtorName,
    string? DebtorPhone,
    Guid? CustomerId,
    DateTime CreatedAt,
    int DaysOutstanding,
    Guid BranchId,
    string BranchName,
    InvoiceType InvoiceType);

public record ReceivableSummaryDto(
    decimal OutstandingTotal,
    int OutstandingCount);

public record CollectReceivableRequest(
    PaymentMethod CollectionMethod,
    string? ProofFileUrl);

public interface IReceivableService
{
    Task<IReadOnlyList<ReceivableDto>> GetAllAsync(Guid? customerId = null, CancellationToken ct = default);
    Task<ReceivableSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<ReceivableDto> CollectAsync(Guid paymentId, CollectReceivableRequest request, CancellationToken ct = default);
}
