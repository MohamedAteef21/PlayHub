namespace PlayHub.Application.Common;

public interface IAuditService
{
    Task LogAsync(
        string actionType,
        string entityType,
        Guid? entityId,
        object? details = null,
        bool success = true,
        CancellationToken ct = default);
}
