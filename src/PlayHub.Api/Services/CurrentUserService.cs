using System.Security.Claims;
using PlayHub.Application.Common;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TenantContext _tenantContext;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, TenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public Guid UserId => _tenantContext.UserId;
    public Guid TenantId => _tenantContext.TenantId;
    public bool IsMaster => _tenantContext.IsMaster;
    public string Email => User?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    public string FullName => User?.Identity?.Name ?? string.Empty;
    public IReadOnlyList<string> Permissions => _tenantContext.Permissions;
    public IReadOnlyList<Guid> BranchIds => User?.FindAll("branch").Select(c => Guid.Parse(c.Value)).ToList() ?? [];

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
}
