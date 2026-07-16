using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Audit;
using PlayHub.Application.Common;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;

    public AuditLogService(PlayHubDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster)
            throw new UnauthorizedAccessException("Activity log is visible to the master user only.");

        var q = _db.AuditLogs.Include(a => a.Branch).AsQueryable();

        // Master Admin sees only their own actions and their staff's; Super Admin sees everything.
        if (!_tenantContext.IsSuperAdmin)
        {
            var me = _tenantContext.UserId;
            q = q.Where(a => a.UserId == me || a.User.ParentUserId == me);
        }

        if (query.UserId.HasValue)
            q = q.Where(a => a.UserId == query.UserId.Value);
        if (query.BranchId.HasValue)
            q = q.Where(a => a.BranchId == query.BranchId.Value);
        if (!string.IsNullOrWhiteSpace(query.ActionType))
            q = q.Where(a => a.ActionType.Contains(query.ActionType));
        if (query.From.HasValue)
            q = q.Where(a => a.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(a => a.Timestamp <= query.To.Value);

        var (page, pageSize, skip) = PagingHelper.Normalize(query.Page, query.PageSize, defaultSize: 50, maxSize: 200);
        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.BranchId, a.Branch != null ? a.Branch.Name : null,
                a.UserId,
                a.UserName != "" ? a.UserName : (a.User.FirstName + " " + a.User.LastName).Trim(),
                a.ActionType, a.EntityType, a.EntityId,
                a.Details, a.Success, a.Timestamp))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>(items, total, page, pageSize);
    }
}
