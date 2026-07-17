using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Auth;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, PlayHubDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
            var isMasterClaim = context.User.FindFirst("is_master")?.Value;
            var branchIdClaim = context.User.FindFirst("branch_id")?.Value
                ?? context.Request.Headers["X-Branch-Id"].FirstOrDefault();

            if (Guid.TryParse(tenantIdClaim, out var tenantId))
                tenantContext.TenantId = tenantId;

            if (Guid.TryParse(userIdClaim, out var userId))
                tenantContext.UserId = userId;

            tenantContext.IsMaster = bool.TryParse(isMasterClaim, out var isMaster) && isMaster;
            // JWT handler maps "role" to ClaimTypes.Role — check both.
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                ?? context.User.FindFirst("role")?.Value;
            tenantContext.Role = int.TryParse(roleClaim, out var roleInt) && Enum.IsDefined(typeof(UserRole), (short)roleInt)
                ? (UserRole)roleInt
                : tenantContext.IsMaster ? UserRole.SuperAdmin : UserRole.Staff;
            tenantContext.Permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();

            if (tenantContext.UserId != Guid.Empty)
            {
                // Branches this user may access (assignments + owned for masters).
                var assigned = await db.UserBranches.IgnoreQueryFilters()
                    .Where(ub => ub.UserId == tenantContext.UserId)
                    .Select(ub => ub.BranchId)
                    .ToListAsync();

                var owned = Array.Empty<Guid>();
                if (tenantContext.IsMaster && !tenantContext.IsSuperAdmin)
                {
                    owned = await db.Branches.IgnoreQueryFilters()
                        .Where(b => b.TenantId == tenantContext.TenantId
                                    && b.OwnerUserId == tenantContext.UserId
                                    && !b.IsDeleted)
                        .Select(b => b.Id)
                        .ToArrayAsync();
                }

                tenantContext.AllowedBranchIds = assigned.Concat(owned).Distinct().ToList();
            }

            if (Guid.TryParse(branchIdClaim, out var branchId))
            {
                // Reject cross-branch access for non-SuperAdmin.
                if (tenantContext.IsSuperAdmin
                    || tenantContext.AllowedBranchIds.Contains(branchId))
                {
                    tenantContext.BranchId = branchId;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "You do not have access to this branch.",
                        code = "BRANCH_FORBIDDEN"
                    });
                    return;
                }
            }

            // Block already-authenticated users when subscription day has passed
            if (tenantContext.UserId != Guid.Empty)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isAuthPath = path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase);

                if (!isAuthPath)
                {
                    var user = await db.Users.IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(u => u.Id == tenantContext.UserId)
                        .Select(u => new { u.IsActive, u.SubscriptionExpiresAt })
                        .FirstOrDefaultAsync();

                    if (user is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { message = "Unauthorized", code = "UNAUTHORIZED" });
                        return;
                    }

                    // IsActive is the gate — Super Admin can force-activate without renewing.
                    if (!user.IsActive)
                    {
                        var expired = AuthService.IsSubscriptionExpired(user.SubscriptionExpiresAt);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            message = expired
                                ? "SUBSCRIPTION_EXPIRED: Your subscription has expired. Please renew your subscription to continue."
                                : "Account is locked. Contact your Super Admin.",
                            code = expired ? "SUBSCRIPTION_EXPIRED" : "ACCOUNT_LOCKED"
                        });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
