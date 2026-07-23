using PlayHub.Domain.Enums;

namespace PlayHub.Application.Reservations;

public record DeviceReservationDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string? RoomName,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid? CustomerId,
    string? CustomerName,
    string? GuestName,
    string? Notes,
    ReservationStatus Status,
    Guid? SessionId,
    string CreatedByName,
    DateTime CreatedAt,
    /// <summary>True when a session opening now would conflict (starts within the next hour).</summary>
    bool WarnWithinOneHour);

public record CreateDeviceReservationRequest(
    Guid DeviceId,
    DateTime StartsAt,
    DateTime? EndsAt = null,
    Guid? CustomerId = null,
    string? GuestName = null,
    string? Notes = null);

public record ReservationConflictDto(
    bool HasConflict,
    Guid? ReservationId,
    string? DeviceName,
    DateTime? StartsAt,
    string? GuestLabel,
    string MessageEn,
    string MessageAr);

public interface IDeviceReservationService
{
    Task<IReadOnlyList<DeviceReservationDto>> GetUpcomingAsync(CancellationToken ct = default);
    Task<DeviceReservationDto> CreateAsync(CreateDeviceReservationRequest request, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task<DeviceReservationDto> MarkStartedAsync(Guid id, Guid sessionId, CancellationToken ct = default);
    Task<ReservationConflictDto> CheckOpenConflictAsync(Guid deviceId, CancellationToken ct = default);
}
