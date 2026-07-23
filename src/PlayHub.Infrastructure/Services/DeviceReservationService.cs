using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Reservations;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class DeviceReservationService : IDeviceReservationService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public DeviceReservationService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DeviceReservationDto>> GetUpcomingAsync(CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var now = DateTime.UtcNow;
        var items = await _db.DeviceReservations
            .Include(r => r.Device).ThenInclude(d => d.Room)
            .Include(r => r.Customer)
            .Include(r => r.CreatedByUser)
            .Where(r => r.BranchId == branchId
                        && r.Status == ReservationStatus.Pending
                        && r.StartsAt >= now.AddHours(-1))
            .OrderBy(r => r.StartsAt)
            .ToListAsync(ct);

        return items.Select(r => Map(r, now)).ToList();
    }

    public async Task<DeviceReservationDto> CreateAsync(CreateDeviceReservationRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        if (request.StartsAt <= DateTime.UtcNow.AddMinutes(-5))
            throw new InvalidOperationException("Reservation start time must be in the future.");

        var device = await _db.Devices
            .Include(d => d.Room)
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        if (!device.IsActive)
            throw new InvalidOperationException("This device is inactive and cannot be reserved.");

        var startsAt = EnsureUtc(request.StartsAt);
        var endsAt = request.EndsAt.HasValue ? EnsureUtc(request.EndsAt.Value) : (DateTime?)null;
        if (endsAt.HasValue && endsAt.Value <= startsAt)
            throw new InvalidOperationException("Reservation end time must be after the start time.");

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(
                c => c.Id == request.CustomerId.Value && c.IsActive, ct)
                ?? throw new KeyNotFoundException("Customer not found.");
        }

        var guestName = string.IsNullOrWhiteSpace(request.GuestName) ? null : request.GuestName.Trim();
        if (customer is null && guestName is null)
            throw new InvalidOperationException("Provide a customer or a guest name for the reservation.");

        // Soft overlap check: another pending reservation on same device within ±30 min window of start.
        var overlap = await _db.DeviceReservations.AnyAsync(r =>
            r.DeviceId == device.Id
            && r.Status == ReservationStatus.Pending
            && r.StartsAt >= startsAt.AddMinutes(-30)
            && r.StartsAt <= startsAt.AddMinutes(30), ct);
        if (overlap)
            throw new InvalidOperationException("This device already has a reservation near that time.");

        var row = new DeviceReservation
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            DeviceId = device.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CustomerId = customer?.Id,
            GuestName = guestName,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = ReservationStatus.Pending,
            CreatedByUserId = _tenantContext.UserId
        };

        _db.DeviceReservations.Add(row);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Device.Reserved", "DeviceReservation", row.Id, new
        {
            device.Name,
            row.StartsAt,
            row.CustomerId,
            row.GuestName
        }, ct: ct);

        await _db.Entry(row).Reference(r => r.CreatedByUser).LoadAsync(ct);
        row.Device = device;
        row.Customer = customer;
        return Map(row, DateTime.UtcNow);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var row = await _db.DeviceReservations
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Reservation not found.");

        if (row.Status != ReservationStatus.Pending)
            throw new InvalidOperationException("Only pending reservations can be cancelled.");

        row.Status = ReservationStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.ReservationCancelled", "DeviceReservation", row.Id, null, ct: ct);
    }

    public async Task<DeviceReservationDto> MarkStartedAsync(Guid id, Guid sessionId, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var row = await _db.DeviceReservations
            .Include(r => r.Device).ThenInclude(d => d.Room)
            .Include(r => r.Customer)
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Reservation not found.");

        if (row.Status != ReservationStatus.Pending)
            throw new InvalidOperationException("Only pending reservations can be started.");

        row.Status = ReservationStatus.Started;
        row.SessionId = sessionId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.ReservationStarted", "DeviceReservation", row.Id, new { sessionId }, ct: ct);
        return Map(row, DateTime.UtcNow);
    }

    public async Task<ReservationConflictDto> CheckOpenConflictAsync(Guid deviceId, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(1);

        var upcoming = await _db.DeviceReservations
            .Include(r => r.Device)
            .Include(r => r.Customer)
            .Where(r => r.BranchId == branchId
                        && r.DeviceId == deviceId
                        && r.Status == ReservationStatus.Pending
                        && r.StartsAt >= now.AddMinutes(-5)
                        && r.StartsAt <= cutoff)
            .OrderBy(r => r.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (upcoming is null)
        {
            return new ReservationConflictDto(
                false, null, null, null, null,
                "No upcoming reservation on this device.",
                "لا يوجد حجز قريب على هذا الجهاز.");
        }

        var guest = upcoming.Customer?.Name
                    ?? upcoming.GuestName
                    ?? "—";
        var startsLocalHint = upcoming.StartsAt.ToString("u");
        return new ReservationConflictDto(
            true,
            upcoming.Id,
            upcoming.Device.Name,
            upcoming.StartsAt,
            guest,
            $"This device is reserved for {guest} starting at {startsLocalHint} (within the next hour).",
            $"هذا الجهاز محجوز لـ {guest} ويبدأ الحجز خلال ساعة أو أقل.");
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static DeviceReservationDto Map(DeviceReservation r, DateTime nowUtc)
    {
        var warn = r.Status == ReservationStatus.Pending
                   && r.StartsAt >= nowUtc.AddMinutes(-5)
                   && r.StartsAt <= nowUtc.AddHours(1);
        return new DeviceReservationDto(
            r.Id,
            r.DeviceId,
            r.Device.Name,
            r.Device.Room?.Name,
            r.StartsAt,
            r.EndsAt,
            r.CustomerId,
            r.Customer?.Name,
            r.GuestName,
            r.Notes,
            r.Status,
            r.SessionId,
            r.CreatedByUser.FullName,
            r.CreatedAt,
            warn);
    }
}
