using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class DeviceMaintenanceService : IDeviceMaintenanceService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAlertDispatcher _alerts;
    private readonly IAuditService _audit;

    public DeviceMaintenanceService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAlertDispatcher alerts,
        IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _alerts = alerts;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DeviceMaintenanceDto>> GetOpenAsync(CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var items = await _db.DeviceMaintenances
            .Include(m => m.Device).ThenInclude(d => d.Room)
            .Include(m => m.ReportedByUser)
            .Where(m => m.BranchId == branchId && m.CompletedAt == null)
            .OrderByDescending(m => m.StartedAt)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DeviceMaintenanceDto>> GetHistoryAsync(int take = 50, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var items = await _db.DeviceMaintenances
            .Include(m => m.Device).ThenInclude(d => d.Room)
            .Include(m => m.ReportedByUser)
            .Where(m => m.BranchId == branchId)
            .OrderByDescending(m => m.StartedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<DeviceMaintenanceDto> StartAsync(StartDeviceMaintenanceRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Maintenance reason is required.");

        var device = await _db.Devices
            .Include(d => d.Room)
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.BranchId == branchId && d.IsActive, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        if (await _db.DeviceMaintenances.AnyAsync(m => m.DeviceId == device.Id && m.CompletedAt == null, ct))
            throw new InvalidOperationException("This device is already in maintenance.");

        if (await _db.Sessions.AnyAsync(s => s.DeviceId == device.Id && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("Close the active session before sending the device to maintenance.");

        var row = new DeviceMaintenance
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            DeviceId = device.Id,
            Reason = request.Reason.Trim(),
            Notes = request.Notes?.Trim(),
            StartedAt = DateTime.UtcNow,
            ReportedByUserId = _tenantContext.UserId
        };

        _db.DeviceMaintenances.Add(row);
        device.IsActive = false;
        await _db.SaveChangesAsync(ct);

        await _alerts.DispatchToMastersAsync(
            _tenantContext.TenantId,
            NotificationType.DeviceMaintenance,
            "Device sent to maintenance",
            "جهاز دخل الصيانة",
            $"{device.Name} ({device.Identifier}) — Reason: {row.Reason}",
            $"{device.Name} ({device.Identifier}) — السبب: {row.Reason}",
            "Device",
            device.Id,
            ct);

        await _audit.LogAsync("Device.MaintenanceStarted", "Device", device.Id, new { row.Reason }, ct: ct);

        await _db.Entry(row).Reference(m => m.ReportedByUser).LoadAsync(ct);
        row.Device = device;
        return Map(row);
    }

    public async Task<DeviceMaintenanceDto> CompleteAsync(
        Guid id, CompleteDeviceMaintenanceRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var row = await _db.DeviceMaintenances
            .Include(m => m.Device).ThenInclude(d => d.Room)
            .Include(m => m.ReportedByUser)
            .FirstOrDefaultAsync(m => m.Id == id && m.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Maintenance record not found.");

        if (row.CompletedAt is not null)
            throw new InvalidOperationException("Maintenance already completed.");

        row.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            row.Notes = string.IsNullOrWhiteSpace(row.Notes)
                ? request.Notes.Trim()
                : $"{row.Notes}\n{request.Notes.Trim()}";

        row.Device.IsActive = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.MaintenanceCompleted", "Device", row.DeviceId, ct: ct);

        return Map(row);
    }

    private static DeviceMaintenanceDto Map(DeviceMaintenance m)
    {
        var end = m.CompletedAt ?? DateTime.UtcNow;
        var days = Math.Max(0, (int)(end.Date - m.StartedAt.Date).TotalDays);
        return new DeviceMaintenanceDto(
            m.Id,
            m.DeviceId,
            m.Device.Name,
            m.Device.Identifier,
            m.Device.Room?.Name,
            m.Reason,
            m.Notes,
            m.StartedAt,
            m.CompletedAt,
            m.ReportedByUser.FullName,
            days);
    }
}
