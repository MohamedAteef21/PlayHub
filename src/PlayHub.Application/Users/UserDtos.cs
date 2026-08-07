using PlayHub.Domain.Enums;

namespace PlayHub.Application.Users;

public record PermissionDto(Guid Id, string Code, string Module, string Action, string Description);

public record ManagedUserDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    bool IsMaster,
    UserRole Role,
    Guid? ParentUserId,
    bool IsActive,
    DateTime? SubscriptionExpiresAt,
    DateTime? SubscriptionLockedAt,
    DateTime? LastLoginAt,
    NotificationChannel AllowedNotificationChannels,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> BranchNames,
    DateTime CreatedAt);

public record CreateUserRequest(
    string Username,
    string Password,
    string FirstName,
    string LastName,
    /// <summary>Preferred: Staff / MasterAdmin / SuperAdmin. Falls back to IsMaster when Role omitted.</summary>
    UserRole? Role,
    bool IsMaster,
    DateTime? SubscriptionExpiresAt,
    NotificationChannel? AllowedNotificationChannels,
    IReadOnlyList<string>? PermissionCodes,
    IReadOnlyList<Guid>? BranchIds);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    bool IsActive,
    UserRole? Role,
    bool IsMaster,
    DateTime? SubscriptionExpiresAt,
    NotificationChannel? AllowedNotificationChannels,
    IReadOnlyList<string>? PermissionCodes,
    IReadOnlyList<Guid>? BranchIds);

public record ResetPasswordRequest(string NewPassword);

public record ResetPasswordResultDto(string NewPassword);

public interface IUserService
{
    Task<PlayHub.Application.Common.PagedResult<ManagedUserDto>> GetUsersAsync(
        int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ManagedUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken ct = default);
    Task<ManagedUserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<ManagedUserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<ResetPasswordResultDto> ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);
}
