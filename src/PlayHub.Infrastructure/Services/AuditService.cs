using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(PlayHubDbContext db, TenantContext tenantContext, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string actionType,
        string entityType,
        Guid? entityId,
        object? details = null,
        bool success = true,
        CancellationToken ct = default)
    {
        if (_tenantContext.TenantId == Guid.Empty || _tenantContext.UserId == Guid.Empty)
            return;

        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
        var givenName = _httpContextAccessor.HttpContext?.User?.FindFirst("given_name")?.Value ?? string.Empty;
        var familyName = _httpContextAccessor.HttpContext?.User?.FindFirst("family_name")?.Value ?? string.Empty;
        var fullName = $"{givenName} {familyName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = userName;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenantContext.TenantId,
            BranchId = _tenantContext.BranchId,
            UserId = _tenantContext.UserId,
            UserName = fullName,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? "{}" : JsonSerializer.Serialize(details),
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Success = success,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
