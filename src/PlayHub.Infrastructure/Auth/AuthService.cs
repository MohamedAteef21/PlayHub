using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayHub.Application.Auth;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly PlayHubDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public AuthService(
        PlayHubDbContext db,
        ITokenService tokenService,
        TenantContext tenantContext,
        IConfiguration configuration)
    {
        _db = db;
        _tokenService = tokenService;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            throw new InvalidOperationException("Tenant slug is already taken.");

        var username = NormalizeUsername(request.Email);
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");

        if (await _db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Email == username, ct))
            throw new InvalidOperationException("Username is already taken.");

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = slug,
            DefaultLanguage = request.DefaultLanguage,
            DefaultCurrency = request.DefaultCurrency,
            BillingRoundUp = false
        };

        var user = new User
        {
            TenantId = tenant.Id,
            Email = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = request.DefaultLanguage
        };
        user.ApplyRole(UserRole.SuperAdmin);

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = request.BranchName.Trim(),
            InvoicePrefix = "INV",
            OwnerUserId = user.Id
        };

        var userBranch = new UserBranch
        {
            UserId = user.Id,
            BranchId = branch.Id,
            IsDefault = true
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        _db.Branches.Add(branch);
        _db.UserBranches.Add(userBranch);

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.UserId = user.Id;
        _tenantContext.IsMaster = true;
        _tenantContext.Role = UserRole.SuperAdmin;
        _tenantContext.BranchId = branch.Id;

        await _db.SaveChangesAsync(ct);

        return await BuildAuthResponseAsync(user, branch.Id, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Email);
        var query = _db.Set<User>().IgnoreQueryFilters()
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Tenant)
            .Where(u => u.Email == username);

        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            query = query.Where(u => u.Tenant.Slug == request.TenantSlug.Trim().ToLowerInvariant());

        var user = await query.FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        // Super Admin controls IsActive independently of expiry (can force-activate after lock).
        // Daily job still deactivates expired accounts; renew or re-activate from Users page.
        if (!user.IsActive)
        {
            if (IsSubscriptionExpired(user.SubscriptionExpiresAt))
            {
                await EnsureSubscriptionNotificationAsync(user, ct);
                await _db.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException(
                    "SUBSCRIPTION_EXPIRED: Your subscription has expired. Please renew your subscription to continue.");
            }

            throw new UnauthorizedAccessException("Account is locked. Contact your Super Admin.");
        }

        _tenantContext.TenantId = user.TenantId;
        _tenantContext.UserId = user.Id;
        _tenantContext.IsMaster = user.IsMaster;
        _tenantContext.Role = user.Role;

        user.LastLoginAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            UserName = $"{user.FirstName} {user.LastName}".Trim(),
            ActionType = "User.LoggedIn",
            EntityType = "User",
            EntityId = user.Id,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        Guid? activeBranchId = null;
        if (user.IsMaster)
        {
            // Prefer explicit assignments; otherwise first branch this master owns (not every tenant branch).
            activeBranchId = user.UserBranches.FirstOrDefault(ub => ub.IsDefault)?.BranchId
                ?? user.UserBranches.FirstOrDefault()?.BranchId
                ?? await _db.Branches
                    .IgnoreQueryFilters()
                    .Where(b => b.TenantId == user.TenantId
                                && b.OwnerUserId == user.Id
                                && b.IsActive
                                && !b.IsDeleted)
                    .OrderBy(b => b.CreatedAt)
                    .Select(b => (Guid?)b.Id)
                    .FirstOrDefaultAsync(ct);
        }
        else if (user.UserBranches.Count == 1)
        {
            activeBranchId = user.UserBranches.First().BranchId;
        }

        return await BuildAuthResponseAsync(user, activeBranchId, ct);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.User).ThenInclude(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .Include(t => t.User).ThenInclude(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Where(t => t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        var storedToken = tokens.FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(request.RefreshToken, t.TokenHash))
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = storedToken.User;
        if (!user.IsActive)
        {
            if (IsSubscriptionExpired(user.SubscriptionExpiresAt))
                throw new UnauthorizedAccessException(
                    "SUBSCRIPTION_EXPIRED: Your subscription has expired. Please renew your subscription to continue.");
            throw new UnauthorizedAccessException("Account is locked. Contact your Super Admin.");
        }

        // Rotate refresh token (sliding session — stays logged in while using the app)
        storedToken.RevokedAt = DateTime.UtcNow;

        _tenantContext.TenantId = user.TenantId;
        _tenantContext.UserId = user.Id;
        _tenantContext.IsMaster = user.IsMaster;
        _tenantContext.Role = user.Role;

        var response = await BuildAuthResponseAsync(user, _tenantContext.BranchId, ct);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.RevokedAt == null)
            .ToListAsync(ct);

        var storedToken = tokens.FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(refreshToken, t.TokenHash));
        if (storedToken is null) return;

        storedToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> SelectBranchAsync(Guid userId, SelectBranchRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!await CanAccessBranchAsync(user, request.BranchId, ct))
            throw new UnauthorizedAccessException("You are not assigned to this branch.");

        _tenantContext.BranchId = request.BranchId;
        return await BuildAuthResponseAsync(user, request.BranchId, ct);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, Guid? activeBranchId, CancellationToken ct)
    {
        var permissions = user.IsMaster
            ? await _db.Permissions.Select(p => p.Code).ToListAsync(ct)
            : user.UserPermissions.Select(up => up.Permission.Code).ToList();

        var branches = await ResolveBranchesAsync(user, ct);

        if (user.IsMaster && activeBranchId.HasValue)
            _tenantContext.BranchId = activeBranchId;

        var branchIds = branches.Select(b => b.Id).ToList();
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(
            user.Id, user.TenantId, user.Email, user.FirstName, user.LastName,
            user.IsMaster, user.Role, permissions, branchIds, activeBranchId);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshDays = int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var days) && days > 0
            ? days
            : 90;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            refreshToken,
            expiresAt,
            MapAuthUser(user, permissions, branches),
            activeBranchId);
    }

    public async Task<AuthUserDto> UpdateUiPreferencesAsync(Guid userId, UpdateUiPreferencesRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (request.PreferredLanguage is not null)
        {
            var lang = request.PreferredLanguage.Trim().ToLowerInvariant();
            if (lang is not ("en" or "ar"))
                throw new InvalidOperationException("Preferred language must be 'en' or 'ar'.");
            user.PreferredLanguage = lang;
        }

        if (request.PreferredTheme is not null)
        {
            var theme = request.PreferredTheme.Trim().ToLowerInvariant();
            if (theme is not ("dark" or "light"))
                throw new InvalidOperationException("Preferred theme must be 'dark' or 'light'.");
            user.PreferredTheme = theme;
        }

        await _db.SaveChangesAsync(ct);

        var permissions = user.IsMaster
            ? await _db.Permissions.Select(p => p.Code).ToListAsync(ct)
            : user.UserPermissions.Select(up => up.Permission.Code).ToList();

        var branches = await ResolveBranchesAsync(user, ct);

        return MapAuthUser(user, permissions, branches);
    }

    private static AuthUserDto MapAuthUser(User user, IReadOnlyList<string> permissions, IReadOnlyList<BranchDto> branches) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsMaster,
            user.Role,
            user.PreferredLanguage,
            user.PreferredTheme,
            user.SubscriptionExpiresAt,
            permissions,
            branches);

    /// <summary>
    /// Masters with assigned branches see only those.
    /// Masters with no assignments see only branches they own (OwnerUserId) — not the whole tenant —
    /// so a new Master Admin starts with zero branches until they create their own.
    /// Super Admin (no owner filter needed) still sees all tenant branches when unassigned.
    /// </summary>
    private async Task<List<BranchDto>> ResolveBranchesAsync(User user, CancellationToken ct)
    {
        var assigned = user.UserBranches
            .Where(ub => ub.Branch is { IsActive: true, IsDeleted: false })
            .Select(ub => new BranchDto(ub.Branch.Id, ub.Branch.Name, ub.IsDefault))
            .ToList();

        if (!user.IsMaster)
            return assigned;

        if (assigned.Count > 0)
            return assigned;

        // Super Admin: keep visibility of all tenant branches when unassigned.
        if (user.Role == UserRole.SuperAdmin)
        {
            return await _db.Branches.IgnoreQueryFilters()
                .Where(b => b.TenantId == user.TenantId && b.IsActive && !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new BranchDto(b.Id, b.Name, false))
                .ToListAsync(ct);
        }

        // Master Admin: only their own venues.
        return await _db.Branches.IgnoreQueryFilters()
            .Where(b => b.TenantId == user.TenantId
                        && b.OwnerUserId == user.Id
                        && b.IsActive
                        && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, false))
            .ToListAsync(ct);
    }

    private async Task<bool> CanAccessBranchAsync(User user, Guid branchId, CancellationToken ct)
    {
        if (user.UserBranches.Any(ub => ub.BranchId == branchId))
            return true;

        if (!user.IsMaster)
            return false;

        if (user.Role == UserRole.SuperAdmin && user.UserBranches.Count == 0)
            return await _db.Branches.IgnoreQueryFilters()
                .AnyAsync(b => b.Id == branchId && b.TenantId == user.TenantId && !b.IsDeleted, ct);

        // Master Admin may access branches they own even before UserBranch rows exist.
        return await _db.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == branchId
                           && b.TenantId == user.TenantId
                           && b.OwnerUserId == user.Id
                           && !b.IsDeleted, ct);
    }

    /// <summary>
    /// Login identifier stored in User.Email. Accepts usernames (e.g. "khaled fawzy"), not only emails.
    /// </summary>
    private static string NormalizeUsername(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static bool IsSubscriptionExpired(DateTime? subscriptionExpiresAt) =>
        subscriptionExpiresAt.HasValue && subscriptionExpiresAt.Value.Date < DateTime.UtcNow.Date;

    private async Task EnsureSubscriptionNotificationAsync(User user, CancellationToken ct)
    {
        var alreadyNotified = await _db.Notifications.IgnoreQueryFilters().AnyAsync(n =>
            n.UserId == user.Id &&
            n.Type == NotificationType.SubscriptionExpired &&
            n.CreatedAt >= DateTime.UtcNow.Date, ct);

        if (alreadyNotified) return;

        _db.Notifications.Add(new Notification
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Type = NotificationType.SubscriptionExpired,
            Title = "Subscription expired",
            TitleAr = "انتهى الاشتراك",
            Message = "Your subscription has expired. Please renew your subscription to continue.",
            MessageAr = "انتهى اشتراكك. جدّد الاشتراك عشان تقدر تستخدم النظام تاني.",
            RelatedEntityType = "User",
            RelatedEntityId = user.Id
        });
    }
}
