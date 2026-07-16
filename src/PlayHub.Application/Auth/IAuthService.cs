using PlayHub.Application.Auth;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<AuthResponse> SelectBranchAsync(Guid userId, SelectBranchRequest request, CancellationToken ct = default);
    Task<AuthUserDto> UpdateUiPreferencesAsync(Guid userId, UpdateUiPreferencesRequest request, CancellationToken ct = default);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(
        Guid userId, Guid tenantId, string email, string firstName, string lastName,
        bool isMaster, UserRole role, IEnumerable<string> permissions, IEnumerable<Guid> branchIds, Guid? activeBranchId);
    string GenerateRefreshToken();
    string HashToken(string token);
}
