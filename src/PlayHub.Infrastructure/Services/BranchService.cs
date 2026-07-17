using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Branches;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public BranchService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<BranchDetailDto>> GetAllAsync(CancellationToken ct = default)
    {
        var query = _db.Branches
            .Include(b => b.PaymentAccounts)
            .Include(b => b.OwnerUser)
            .AsQueryable();

        // SuperAdmin sees all; Master/Staff only their allowed branches.
        if (!_tenantContext.IsSuperAdmin)
            query = query.Where(b => _tenantContext.AllowedBranchIds.Contains(b.Id));

        var branches = await query.OrderBy(b => b.Name).ToListAsync(ct);
        return branches.Select(ToDto).ToList();
    }

    public async Task<BranchDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!_tenantContext.IsSuperAdmin && !_tenantContext.AllowedBranchIds.Contains(id))
            return null;

        var branch = await _db.Branches
            .Include(b => b.PaymentAccounts)
            .Include(b => b.OwnerUser)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        return branch is null ? null : ToDto(branch);
    }

    public async Task<BranchDetailDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Only the master user can create branches.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Branch name is required.");

        var prefix = string.IsNullOrWhiteSpace(request.InvoicePrefix)
            ? GeneratePrefix(request.Name)
            : request.InvoicePrefix.Trim().ToUpperInvariant();
        if (prefix.Length > 20)
            prefix = prefix[..20];

        var branch = new Branch
        {
            TenantId = _tenantContext.TenantId,
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            OwnerUserId = _tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId,
            InvoicePrefix = prefix,
            PaymentAccounts = []
        };

        _db.Branches.Add(branch);

        // Always link the creating master to the new branch so it appears in their list.
        if (_tenantContext.UserId != Guid.Empty && _tenantContext.IsMaster)
        {
            var alreadyLinked = await _db.UserBranches.AnyAsync(
                ub => ub.UserId == _tenantContext.UserId && ub.BranchId == branch.Id, ct);
            if (!alreadyLinked)
            {
                var hasAny = await _db.UserBranches.AnyAsync(ub => ub.UserId == _tenantContext.UserId, ct);
                _db.UserBranches.Add(new UserBranch
                {
                    UserId = _tenantContext.UserId,
                    BranchId = branch.Id,
                    IsDefault = !hasAny
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Branch.Created", "Branch", branch.Id, new { branch.Name }, ct: ct);

        // So the rest of this request (and immediate UI refresh) can see the new branch.
        if (!_tenantContext.AllowedBranchIds.Contains(branch.Id))
            _tenantContext.AllowedBranchIds.Add(branch.Id);

        var created = await _db.Branches
            .AsNoTracking()
            .Include(b => b.PaymentAccounts)
            .Include(b => b.OwnerUser)
            .FirstOrDefaultAsync(b => b.Id == branch.Id, ct);

        return ToDto(created ?? branch);
    }

    public async Task<BranchDetailDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken ct = default)
    {
        if (!_tenantContext.IsSuperAdmin && !_tenantContext.AllowedBranchIds.Contains(id))
            throw new UnauthorizedAccessException("You can only update your own branches.");

        var branch = await _db.Branches
            .Include(b => b.PaymentAccounts)
            .Include(b => b.OwnerUser)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException("Branch not found.");
        if (_tenantContext.IsMaster)
        {
            branch.Name = request.Name.Trim();
            branch.Address = request.Address?.Trim();
            branch.Phone = request.Phone?.Trim();
            if (!string.IsNullOrWhiteSpace(request.InvoicePrefix))
                branch.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
            branch.IsActive = request.IsActive;
        }

        if (request.PaymentAccounts is not null)
            ReplacePaymentAccounts(branch, request.PaymentAccounts);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Branch.Updated", "Branch", branch.Id, new { branch.Name, branch.IsActive }, ct: ct);

        return ToDto(branch);
    }

    private void ReplacePaymentAccounts(Branch branch, IReadOnlyList<BranchPaymentAccountInput> inputs)
    {
        if (branch.PaymentAccounts.Count > 0)
            _db.BranchPaymentAccounts.RemoveRange(branch.PaymentAccounts);
        branch.PaymentAccounts.Clear();

        var order = 0;
        foreach (var input in inputs)
        {
            var number = input.AccountNumber?.Trim();
            if (string.IsNullOrWhiteSpace(number)) continue;
            if (input.AccountType is not (PaymentAccountType.BankTransfer or PaymentAccountType.DigitalWallet))
                throw new InvalidOperationException("Invalid payment account type.");

            branch.PaymentAccounts.Add(new BranchPaymentAccount
            {
                TenantId = branch.TenantId,
                BranchId = branch.Id,
                AccountType = input.AccountType,
                Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim(),
                AccountNumber = number,
                SortOrder = input.SortOrder != 0 ? input.SortOrder : order++,
                IsActive = input.IsActive
            });
        }
    }

    private static BranchDetailDto ToDto(Branch b)
    {
        var paymentAccounts = b.PaymentAccounts ?? [];
        var accounts = paymentAccounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.AccountType)
            .Select(a => new BranchPaymentAccountDto(a.Id, a.AccountType, a.Label, a.AccountNumber, a.SortOrder, a.IsActive))
            .ToList();

        // Fallback to legacy single fields until settings are re-saved
        if (accounts.Count == 0)
            accounts = LegacyAccounts(b);

        var ownerName = b.OwnerUser is null
            ? null
            : $"{b.OwnerUser.FirstName} {b.OwnerUser.LastName}".Trim();

        return new(
            b.Id,
            b.Name,
            b.Address,
            b.Phone,
            b.InvoicePrefix,
            b.IsActive,
            b.OwnerUserId,
            string.IsNullOrWhiteSpace(ownerName) ? b.OwnerUser?.Email : ownerName,
            accounts,
            b.CreatedAt);
    }

    private static List<BranchPaymentAccountDto> LegacyAccounts(Branch b)
    {
        var list = new List<BranchPaymentAccountDto>();
        if (b.UseSharedTransferAccount && !string.IsNullOrWhiteSpace(b.SharedTransferAccount))
        {
            list.Add(new(Guid.Empty, PaymentAccountType.BankTransfer, "Shared", b.SharedTransferAccount!, 0, true));
            list.Add(new(Guid.Empty, PaymentAccountType.DigitalWallet, "Shared", b.SharedTransferAccount!, 1, true));
            return list;
        }

        if (!string.IsNullOrWhiteSpace(b.BankTransferAccount))
            list.Add(new(Guid.Empty, PaymentAccountType.BankTransfer, null, b.BankTransferAccount!, 0, true));
        if (!string.IsNullOrWhiteSpace(b.DigitalWalletAccount))
            list.Add(new(Guid.Empty, PaymentAccountType.DigitalWallet, null, b.DigitalWalletAccount!, 1, true));
        return list;
    }

    private static string GeneratePrefix(string name)
    {
        var chars = name.Where(char.IsLetterOrDigit).Take(3).ToArray();
        return chars.Length > 0 ? new string(chars).ToUpperInvariant() : "INV";
    }
}
