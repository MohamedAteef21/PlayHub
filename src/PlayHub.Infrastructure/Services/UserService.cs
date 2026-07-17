using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Users;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public UserService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<PagedResult<ManagedUserDto>> GetUsersAsync(
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        EnsureCanManageUsers();
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .Where(u => u.TenantId == _tenantContext.TenantId && !u.IsDeleted);

        // Only Super Admin sees everyone; everyone else sees themselves + staff they created.
        if (!_tenantContext.IsSuperAdmin)
            query = query.Where(u => u.Id == _tenantContext.UserId || u.ParentUserId == _tenantContext.UserId);

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.FirstName)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<ManagedUserDto>(users.Select(Map).ToList(), total, p, size);
    }

    public async Task<ManagedUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureCanManageUsers();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId && !u.IsDeleted, ct);

        if (user is null) return null;
        EnsureCanAccessUser(user);
        return Map(user);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken ct = default)
    {
        return await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Action)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Module, p.Action, p.Description))
            .ToListAsync(ct);
    }

    public async Task<ManagedUserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        EnsureCanManageUsers();

        var username = NormalizeUsername(request.Username);
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == username && !u.IsDeleted, ct))
            throw new InvalidOperationException("Username is already taken.");

        // Role is fixed by who creates: SuperAdmin creates Master Admins, Master Admin creates Staff.
        var role = _tenantContext.IsSuperAdmin ? UserRole.MasterAdmin : UserRole.Staff;

        var user = new User
        {
            TenantId = _tenantContext.TenantId,
            Email = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = "en"
        };
        user.ApplyRole(role);

        if (role == UserRole.Staff)
            user.ParentUserId = _tenantContext.UserId;

        // Only SuperAdmin controls subscription dates + notification channels for masters
        if (_tenantContext.IsSuperAdmin)
        {
            user.SubscriptionExpiresAt = NormalizeExpiryDate(request.SubscriptionExpiresAt);
            if (role is UserRole.MasterAdmin or UserRole.SuperAdmin)
            {
                user.AllowedNotificationChannels = request.AllowedNotificationChannels
                    ?? (NotificationChannel.Email | NotificationChannel.WhatsApp);
            }
        }
        else if (role == UserRole.Staff)
        {
            // Staff belong to their master: they inherit the master's subscription end date.
            user.SubscriptionExpiresAt = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == _tenantContext.UserId)
                .Select(u => u.SubscriptionExpiresAt)
                .FirstOrDefaultAsync(ct);
        }

        _db.Users.Add(user);

        if (role == UserRole.Staff)
            await AssignPermissionsAsync(user, request.PermissionCodes, ct);

        // Master/SuperAdmin may start with no branches — they create their own later.
        // Staff must be assigned at least one branch.
        await AssignBranchesAsync(user, request.BranchIds, requireAtLeastOne: role == UserRole.Staff, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("User.Created", "User", user.Id, new { user.Email, user.Role, user.IsMaster }, ct: ct);

        return (await GetByIdAsync(user.Id, ct))!;
    }

    public async Task<ManagedUserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        EnsureCanManageUsers();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserPermissions)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException("User not found.");

        EnsureCanAccessUser(user);

        if (user.Id == _tenantContext.UserId && !request.IsActive)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        // Roles are fixed after creation.
        var role = user.Role;

        if (user.Role == UserRole.SuperAdmin && !_tenantContext.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only Super Admin can edit Super Admins.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.ApplyRole(role);

        if (role == UserRole.Staff && user.ParentUserId is null)
            user.ParentUserId = _tenantContext.UserId;
        if (role != UserRole.Staff)
            user.ParentUserId = null;

        if (_tenantContext.IsSuperAdmin)
        {
            user.SubscriptionExpiresAt = NormalizeExpiryDate(request.SubscriptionExpiresAt);
            user.IsActive = request.IsActive;
            if (request.IsActive)
                user.SubscriptionLockedAt = null;
            else if (IsSubscriptionExpired(user.SubscriptionExpiresAt))
                user.SubscriptionLockedAt ??= DateTime.UtcNow;

            if ((role is UserRole.MasterAdmin or UserRole.SuperAdmin) && request.AllowedNotificationChannels.HasValue)
                user.AllowedNotificationChannels = request.AllowedNotificationChannels.Value;

            // Staff belong to their master: cascade the master's expiry (and unlock) to their staff.
            if (role == UserRole.MasterAdmin)
            {
                var staff = await _db.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.ParentUserId == user.Id && !u.IsDeleted)
                    .ToListAsync(ct);

                foreach (var s in staff)
                {
                    s.SubscriptionExpiresAt = user.SubscriptionExpiresAt;
                    if (user.IsActive && s.SubscriptionLockedAt != null && !IsSubscriptionExpired(s.SubscriptionExpiresAt))
                    {
                        s.IsActive = true;
                        s.SubscriptionLockedAt = null;
                    }
                    else if (!user.IsActive)
                    {
                        s.IsActive = false;
                        s.SubscriptionLockedAt ??= user.SubscriptionLockedAt ?? DateTime.UtcNow;
                    }
                }
            }
        }
        else
        {
            // Master Admin can activate/deactivate their staff only
            if (role == UserRole.Staff)
            {
                user.IsActive = request.IsActive;

                // Keep staff expiry in sync with their master's subscription.
                user.SubscriptionExpiresAt = await _db.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.Id == _tenantContext.UserId)
                    .Select(u => u.SubscriptionExpiresAt)
                    .FirstOrDefaultAsync(ct);
            }
        }

        _db.UserPermissions.RemoveRange(user.UserPermissions);
        _db.UserBranches.RemoveRange(user.UserBranches);
        await _db.SaveChangesAsync(ct);

        user.UserPermissions.Clear();
        user.UserBranches.Clear();

        if (user.Role == UserRole.Staff)
            await AssignPermissionsAsync(user, request.PermissionCodes, ct);

        await AssignBranchesAsync(user, request.BranchIds, requireAtLeastOne: role == UserRole.Staff, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("User.Updated", "User", user.Id, new { user.Email, user.Role, user.IsMaster, user.IsActive }, ct: ct);

        return (await GetByIdAsync(user.Id, ct))!;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureCanManageUsers();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException("User not found.");

        EnsureCanAccessUser(user);

        if (user.Id == _tenantContext.UserId)
            throw new InvalidOperationException("You cannot delete your own account.");

        if (user.Role == UserRole.SuperAdmin)
            throw new InvalidOperationException("Cannot delete a Super Admin account.");

        user.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        user.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("User.SoftDeleted", "User", user.Id, new { user.Email, user.Role }, ct: ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        EnsureCanManageUsers();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId && !u.IsDeleted, ct)
            ?? throw new KeyNotFoundException("User not found.");

        EnsureCanAccessUser(user);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("User.PasswordReset", "User", user.Id, new { user.Email }, ct: ct);
    }

    private async Task AssignPermissionsAsync(User user, IReadOnlyList<string>? codes, CancellationToken ct)
    {
        if (codes is null || codes.Count == 0) return;

        var permissions = await _db.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToListAsync(ct);

        foreach (var permission in permissions)
        {
            _db.UserPermissions.Add(new UserPermission
            {
                UserId = user.Id,
                PermissionId = permission.Id
            });
        }
    }

    private async Task AssignBranchesAsync(User user, IReadOnlyList<Guid>? branchIds, bool requireAtLeastOne, CancellationToken ct)
    {
        if (branchIds is null || branchIds.Count == 0)
        {
            if (requireAtLeastOne)
                throw new InvalidOperationException("At least one branch is required.");
            return;
        }

        var validBranchIds = await _db.Branches
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == _tenantContext.TenantId && !b.IsDeleted && branchIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (validBranchIds.Count == 0)
            throw new InvalidOperationException("No valid branches selected.");

        // Master Admin can only assign branches they own (or are assigned to).
        if (_tenantContext.Role == UserRole.MasterAdmin)
        {
            var myBranchIds = await _db.Branches.IgnoreQueryFilters()
                .Where(b => b.TenantId == _tenantContext.TenantId
                            && !b.IsDeleted
                            && (b.OwnerUserId == _tenantContext.UserId
                                || _db.UserBranches.Any(ub => ub.UserId == _tenantContext.UserId && ub.BranchId == b.Id)))
                .Select(b => b.Id)
                .ToListAsync(ct);

            validBranchIds = validBranchIds.Where(id => myBranchIds.Contains(id)).ToList();

            if (validBranchIds.Count == 0)
                throw new InvalidOperationException("You can only assign branches you have access to.");
        }

        var first = true;
        foreach (var branchId in validBranchIds)
        {
            _db.UserBranches.Add(new UserBranch
            {
                UserId = user.Id,
                BranchId = branchId,
                IsDefault = first
            });
            first = false;
        }
    }

    private void EnsureCanManageUsers()
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Only Super Admin or Master Admin can manage users.");
    }

    private void EnsureCanAccessUser(User user)
    {
        if (_tenantContext.IsSuperAdmin) return;
        if (user.Id == _tenantContext.UserId) return;
        if (user.ParentUserId == _tenantContext.UserId) return;
        throw new UnauthorizedAccessException("You can only manage users under your account.");
    }

    private static ManagedUserDto Map(User user) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsMaster,
            user.Role,
            user.ParentUserId,
            user.IsActive,
            user.SubscriptionExpiresAt,
            user.SubscriptionLockedAt,
            user.LastLoginAt,
            user.AllowedNotificationChannels,
            user.IsMaster
                ? ["*"]
                : user.UserPermissions.Select(up => up.Permission.Code).OrderBy(c => c).ToList(),
            user.UserBranches.Select(ub => ub.BranchId).ToList(),
            user.UserBranches
                .OrderByDescending(ub => ub.IsDefault)
                .Select(ub => ub.Branch.Name)
                .ToList(),
            user.CreatedAt);

    private static DateTime? NormalizeExpiryDate(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;

    private static bool IsSubscriptionExpired(DateTime? subscriptionExpiresAt) =>
        subscriptionExpiresAt.HasValue && subscriptionExpiresAt.Value.Date < DateTime.UtcNow.Date;

    private static string NormalizeUsername(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
