namespace PlayHub.Infrastructure.Data;

using PlayHub.Domain.Enums;

public partial class TenantContext
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsMaster { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    public Guid? BranchId { get; set; }
    /// <summary>Branches this user may access (UserBranches + owned). Empty = no branch data for non-SuperAdmin.</summary>
    public List<Guid> AllowedBranchIds { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;
    public bool IsMasterAdmin => Role == UserRole.MasterAdmin;

    /// <summary>
    /// SuperAdmin with no active branch sees the whole tenant.
    /// Everyone else only sees the active branch, or (if none selected) their allowed branches.
    /// </summary>
    public bool CanSeeBranch(Guid branchId) =>
        IsSuperAdmin
            ? BranchId is null || BranchId == branchId
            : BranchId is not null
                ? BranchId == branchId
                : AllowedBranchIds.Contains(branchId);
}
