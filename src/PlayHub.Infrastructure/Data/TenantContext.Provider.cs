using PlayHub.Application.Common;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Data;

public partial class TenantContext : ITenantProvider, IBranchProvider
{
    public void SetBranchId(Guid branchId) => BranchId = branchId;
}
