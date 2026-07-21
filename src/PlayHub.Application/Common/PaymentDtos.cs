using PlayHub.Domain.Enums;

namespace PlayHub.Application.Common;

public record PaymentRequest(
    PaymentMethod PaymentMethod,
    string? DebtorName,
    string? DebtorPhone,
    string? ProofFileUrl,
    Guid? CustomerId = null);

public static class PaymentValidator
{
    public static void Validate(PaymentRequest payment)
    {
        if (payment.PaymentMethod == PaymentMethod.Deferred && string.IsNullOrWhiteSpace(payment.DebtorName))
            throw new InvalidOperationException("Debtor name is required for deferred payments.");
        // Bank transfer / wallet: receipt image is optional.
    }
}
