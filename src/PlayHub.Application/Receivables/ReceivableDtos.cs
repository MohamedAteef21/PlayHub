using PlayHub.Domain.Enums;

namespace PlayHub.Application.Receivables;

public record ReceivableDto(
    Guid PaymentId,
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string DebtorName,
    string? DebtorPhone,
    DateTime CreatedAt,
    int DaysOutstanding,
    Guid BranchId,
    string BranchName,
    InvoiceType InvoiceType);

public record CollectReceivableRequest(
    PaymentMethod CollectionMethod,
    string? ProofFileUrl);

public interface IReceivableService
{
    Task<IReadOnlyList<ReceivableDto>> GetAllAsync(CancellationToken ct = default);
    Task<ReceivableDto> CollectAsync(Guid paymentId, CollectReceivableRequest request, CancellationToken ct = default);
}
