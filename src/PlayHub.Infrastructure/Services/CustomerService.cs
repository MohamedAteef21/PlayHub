using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Customers;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public CustomerService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<PagedResult<CustomerDto>> SearchAsync(
        string? q = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phoneDigits = PhoneNormalizer.Normalize(term);
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.Code.Contains(term) ||
                (phoneDigits.Length > 0 && c.Phone.Contains(phoneDigits)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<CustomerDto>(items.Select(Map).ToList(), total, p, size);
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? null : Map(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Customer name is required.");

        var phone = PhoneNormalizer.Normalize(request.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Customer phone is required.");

        if (await _db.Customers.AnyAsync(c => c.Phone == phone, ct))
            throw new InvalidOperationException("A customer with this phone already exists.");

        var tenant = await _db.Tenants.FirstAsync(t => t.Id == _tenantContext.TenantId, ct);
        var number = tenant.NextCustomerNumber < 1 ? 1 : tenant.NextCustomerNumber;
        tenant.NextCustomerNumber = number + 1;

        var customer = new Customer
        {
            TenantId = _tenantContext.TenantId,
            Code = $"C{number:D5}",
            Name = name,
            Phone = phone,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = request.IsActive
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Customer.Created", "Customer", customer.Id, new
        {
            customer.Code,
            customer.Name,
            customer.Phone
        }, ct: ct);

        return Map(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Customer name is required.");

        var phone = PhoneNormalizer.Normalize(request.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Customer phone is required.");

        if (await _db.Customers.AnyAsync(c => c.Phone == phone && c.Id != id, ct))
            throw new InvalidOperationException("A customer with this phone already exists.");

        customer.Name = name;
        customer.Phone = phone;
        customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        customer.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Customer.Updated", "Customer", customer.Id, new
        {
            customer.Code,
            customer.Name,
            customer.Phone,
            customer.IsActive
        }, ct: ct);

        return Map(customer);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        customer.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        customer.IsActive = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Customer.SoftDeleted", "Customer", customer.Id, new
        {
            customer.Code,
            customer.Name
        }, ct: ct);
    }

    public async Task<CustomerDto> TopUpWalletAsync(Guid id, WalletTopUpRequest request, CancellationToken ct = default)
    {
        var amount = decimal.Round(request.Amount, 2);
        var bonus = decimal.Round(request.BonusAmount, 2);
        if (amount <= 0)
            throw new InvalidOperationException("Top-up amount must be greater than zero.");
        if (bonus < 0)
            throw new InvalidOperationException("Bonus cannot be negative.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        customer.WalletBalance += amount;
        _db.WalletTransactions.Add(new WalletTransaction
        {
            TenantId = _tenantContext.TenantId,
            BranchId = _tenantContext.BranchId,
            CustomerId = customer.Id,
            Type = WalletTransactionType.TopUp,
            Amount = amount,
            BalanceAfter = customer.WalletBalance,
            Note = note,
            CreatedByUserId = _tenantContext.UserId
        });

        if (bonus > 0)
        {
            customer.WalletBalance += bonus;
            _db.WalletTransactions.Add(new WalletTransaction
            {
                TenantId = _tenantContext.TenantId,
                CustomerId = customer.Id,
                Type = WalletTransactionType.Bonus,
                Amount = bonus,
                BalanceAfter = customer.WalletBalance,
                Note = note,
                CreatedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Customer.WalletTopUp", "Customer", customer.Id, new
        {
            customer.Code,
            Paid = amount,
            Bonus = bonus,
            customer.WalletBalance
        }, ct: ct);

        return Map(customer);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(
        Guid id, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == id, ct))
            throw new KeyNotFoundException("Customer not found.");

        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);
        var query = _db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(size)
            .Select(t => new WalletTransactionDto(
                t.Id, (short)t.Type, t.Amount, t.BalanceAfter, t.Note, t.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<WalletTransactionDto>(items, total, p, size);
    }

    private static CustomerDto Map(Customer c) =>
        new(c.Id, c.Code, c.Name, c.Phone, c.Notes, c.WalletBalance, c.IsActive, c.CreatedAt);
}
