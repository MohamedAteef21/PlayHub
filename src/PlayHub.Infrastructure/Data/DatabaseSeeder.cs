using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Data;

public static class DatabaseSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DefaultBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DefaultUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(PlayHubDbContext db, IConfiguration configuration, CancellationToken ct = default)
    {
        if (!configuration.GetValue("Seed:Enabled", true))
            return;

        var email = configuration["Seed:Email"] ?? "PlayHubAdmin";
        var password = configuration["Seed:Password"] ?? "Admin@123";
        var tenantName = configuration["Seed:TenantName"] ?? "PlayHub";
        var tenantSlug = (configuration["Seed:TenantSlug"] ?? "playhub").Trim().ToLowerInvariant();
        var branchName = configuration["Seed:BranchName"] ?? "Main Branch";
        var firstName = configuration["Seed:FirstName"] ?? "PlayHub";
        var lastName = configuration["Seed:LastName"] ?? "Admin";

        var username = string.Join(' ', email.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == username, ct);
        if (existing is not null)
        {
            // Optional one-shot ops reset for known seed account (default off).
            if (configuration.GetValue("Seed:ForceResetPassword", false))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                existing.IsActive = true;
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var seedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenant = new Tenant
        {
            Id = DefaultTenantId,
            Name = tenantName,
            Slug = tenantSlug,
            DefaultLanguage = "en",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            BillingRoundUp = false,
            IsActive = true,
            CreatedAt = seedTime
        };

        var branch = new Branch
        {
            Id = DefaultBranchId,
            TenantId = tenant.Id,
            Name = branchName,
            InvoicePrefix = "INV",
            NextInvoiceNumber = 1,
            IsActive = true,
            OwnerUserId = DefaultUserId,
            CreatedAt = seedTime
        };

        var user = new User
        {
            Id = DefaultUserId,
            TenantId = tenant.Id,
            Email = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            PreferredLanguage = "en",
            IsActive = true,
            CreatedAt = seedTime
        };
        user.ApplyRole(Domain.Enums.UserRole.SuperAdmin);

        var userBranch = new UserBranch
        {
            UserId = user.Id,
            BranchId = branch.Id,
            IsDefault = true
        };

        db.Tenants.Add(tenant);
        db.Branches.Add(branch);
        db.Users.Add(user);
        db.UserBranches.Add(userBranch);

        await db.SaveChangesAsync(ct);
    }
}
