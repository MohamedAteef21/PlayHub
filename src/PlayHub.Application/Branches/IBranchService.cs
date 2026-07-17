using PlayHub.Application.Branches;

namespace PlayHub.Application.Branches;

public interface IBranchService
{
    Task<IReadOnlyList<BranchDetailDto>> GetAllAsync(CancellationToken ct = default);
    Task<BranchDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BranchDetailDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default);
    Task<BranchDetailDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
