namespace PlayHub.Infrastructure.Data;

using PlayHub.Domain.Enums;

public partial class TenantContext
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsMaster { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    public Guid? BranchId { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;
    public bool IsMasterAdmin => Role == UserRole.MasterAdmin;
}
