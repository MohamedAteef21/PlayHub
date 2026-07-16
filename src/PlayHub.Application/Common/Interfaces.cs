namespace PlayHub.Application.Common;

public interface ITenantProvider
{
    Guid TenantId { get; }
    Guid UserId { get; }
    bool IsMaster { get; }
    IReadOnlyList<string> Permissions { get; }
}

public interface IBranchProvider
{
    Guid? BranchId { get; }
    void SetBranchId(Guid branchId);
}

public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    bool IsMaster { get; }
    string Email { get; }
    string FullName { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<Guid> BranchIds { get; }
}
