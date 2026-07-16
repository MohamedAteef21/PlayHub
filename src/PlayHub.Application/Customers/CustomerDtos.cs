using PlayHub.Application.Common;

namespace PlayHub.Application.Customers;

public record CustomerDto(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    string? Notes,
    decimal WalletBalance,
    bool IsActive,
    DateTime CreatedAt);

public record WalletTopUpRequest(
    decimal Amount,
    /// <summary>Free extra credit granted with this top-up (pay 500 → get 550). Logged separately.</summary>
    decimal BonusAmount = 0,
    string? Note = null);

public record WalletTransactionDto(
    Guid Id,
    short Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Note,
    DateTime CreatedAt);

public record CreateCustomerRequest(
    string Name,
    string Phone,
    string? Notes = null,
    bool IsActive = true);

public record UpdateCustomerRequest(
    string Name,
    string Phone,
    string? Notes,
    bool IsActive);

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> SearchAsync(
        string? q = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> TopUpWalletAsync(Guid id, WalletTopUpRequest request, CancellationToken ct = default);
    Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(
        Guid id, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
