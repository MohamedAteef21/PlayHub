using PlayHub.Domain.Enums;

namespace PlayHub.Application.Auth;

public record RegisterTenantRequest(
    string TenantName,
    string Slug,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string BranchName,
    string DefaultLanguage = "en",
    string DefaultCurrency = "EGP");

public record LoginRequest(string Email, string Password, string? TenantSlug = null);

public record RefreshTokenRequest(string RefreshToken);

public record SelectBranchRequest(Guid BranchId);

public record AuthUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsMaster,
    UserRole Role,
    string? PreferredLanguage,
    string? PreferredTheme,
    DateTime? SubscriptionExpiresAt,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<BranchDto> Branches);

public record BranchDto(Guid Id, string Name, bool IsDefault);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthUserDto User,
    Guid? ActiveBranchId);

public record UpdateUiPreferencesRequest(string? PreferredLanguage, string? PreferredTheme);

public record TenantDto(Guid Id, string Name, string Slug, string DefaultLanguage, string DefaultCurrency, bool BillingRoundUp);
