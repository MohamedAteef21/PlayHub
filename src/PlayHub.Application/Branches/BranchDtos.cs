using PlayHub.Domain.Enums;

namespace PlayHub.Application.Branches;

public record BranchPaymentAccountDto(
    Guid Id,
    PaymentAccountType AccountType,
    string? Label,
    string AccountNumber,
    int SortOrder,
    bool IsActive);

public record BranchPaymentAccountInput(
    PaymentAccountType AccountType,
    string? Label,
    string AccountNumber,
    int SortOrder = 0,
    bool IsActive = true);

public record BranchDetailDto(
    Guid Id,
    string Name,
    string? Address,
    string? Phone,
    string InvoicePrefix,
    bool IsActive,
    Guid? OwnerUserId,
    string? OwnerName,
    IReadOnlyList<BranchPaymentAccountDto> PaymentAccounts,
    DateTime CreatedAt);

public record CreateBranchRequest(string Name, string? Address, string? Phone, string? InvoicePrefix);

public record UpdateBranchRequest(
    string Name,
    string? Address,
    string? Phone,
    string? InvoicePrefix,
    bool IsActive,
    IReadOnlyList<BranchPaymentAccountInput>? PaymentAccounts = null);
