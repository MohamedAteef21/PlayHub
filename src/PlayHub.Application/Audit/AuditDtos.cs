namespace PlayHub.Application.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid UserId,
    string UserName,
    string ActionType,
    string EntityType,
    Guid? EntityId,
    string Details,
    bool Success,
    DateTime Timestamp);

public record AuditLogQuery(
    Guid? UserId = null,
    Guid? BranchId = null,
    string? ActionType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50);

public interface IAuditLogService
{
    Task<PlayHub.Application.Common.PagedResult<AuditLogDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct = default);
}
