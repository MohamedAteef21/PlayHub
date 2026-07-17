using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Assets;
using PlayHub.Application.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class AssetService : IAssetService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public AssetService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var rooms = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .Where(r => r.BranchId == branchId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return rooms.Select(MapRoom).ToList();
    }

    public async Task<RoomDto?> GetRoomByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var room = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct);

        return room is null ? null : MapRoom(room);
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var room = new Room
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            RoomNumber = request.RoomNumber?.Trim(),
            MaxWatchingCapacity = request.MaxWatchingCapacity,
            VipSurchargePerHour = Math.Max(0, request.VipSurchargePerHour)
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);

        if (request.Assets is { Count: > 0 })
            await ReplaceRoomAssetsAsync(room, request.Assets, ct);

        await _audit.LogAsync("Room.Created", "Room", room.Id, new { room.Name, room.MaxWatchingCapacity }, ct: ct);

        await _db.Entry(room).Collection(r => r.RoomAssets).Query()
            .Include(a => a.VenueAssetType).LoadAsync(ct);
        return MapRoom(room);
    }

    public async Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var room = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        room.Name = request.Name.Trim();
        room.RoomNumber = request.RoomNumber?.Trim();
        room.MaxWatchingCapacity = request.MaxWatchingCapacity;
        room.VipSurchargePerHour = Math.Max(0, request.VipSurchargePerHour);
        room.IsActive = request.IsActive;

        if (request.Assets is not null)
            await ReplaceRoomAssetsAsync(room, request.Assets, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Room.Updated", "Room", room.Id, new { room.Name, room.IsActive }, ct: ct);

        return MapRoom(room);
    }

    public async Task<IReadOnlyList<VenueAssetTypeDto>> GetVenueAssetTypesAsync(CancellationToken ct = default)
    {
        return await _db.VenueAssetTypes
            .OrderBy(c => c.Name)
            .Select(c => new VenueAssetTypeDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<VenueAssetTypeDto> CreateVenueAssetTypeAsync(CreateVenueAssetTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Asset type name is required.");

        var type = new VenueAssetType
        {
            TenantId = _tenantContext.TenantId,
            Name = name,
            Description = request.Description?.Trim()
        };

        _db.VenueAssetTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("VenueAssetType.Created", "VenueAssetType", type.Id, new { type.Name }, ct: ct);

        return new VenueAssetTypeDto(type.Id, type.Name, type.Description, type.IsActive);
    }

    public async Task<VenueAssetTypeDto> UpdateVenueAssetTypeAsync(Guid id, UpdateVenueAssetTypeRequest request, CancellationToken ct = default)
    {
        var type = await _db.VenueAssetTypes.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Venue asset type not found.");

        type.Name = request.Name.Trim();
        type.Description = request.Description?.Trim();
        type.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("VenueAssetType.Updated", "VenueAssetType", type.Id, new { type.Name, type.IsActive }, ct: ct);

        return new VenueAssetTypeDto(type.Id, type.Name, type.Description, type.IsActive);
    }

    public async Task<IReadOnlyList<ControllerTypeDto>> GetControllerTypesAsync(CancellationToken ct = default)
    {
        return await _db.ControllerTypes
            .OrderBy(c => c.Name)
            .Select(c => new ControllerTypeDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ControllerTypeDto> CreateControllerTypeAsync(CreateControllerTypeRequest request, CancellationToken ct = default)
    {
        var type = new ControllerType
        {
            TenantId = _tenantContext.TenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim()
        };

        _db.ControllerTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ControllerType.Created", "ControllerType", type.Id, new { type.Name }, ct: ct);

        return new ControllerTypeDto(type.Id, type.Name, type.Description, type.IsActive);
    }

    public async Task<ControllerTypeDto> UpdateControllerTypeAsync(Guid id, UpdateControllerTypeRequest request, CancellationToken ct = default)
    {
        var type = await _db.ControllerTypes.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Controller type not found.");

        type.Name = request.Name.Trim();
        type.Description = request.Description?.Trim();
        type.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ControllerType.Updated", "ControllerType", type.Id, new { type.Name, type.IsActive }, ct: ct);

        return new ControllerTypeDto(type.Id, type.Name, type.Description, type.IsActive);
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(Guid? roomId = null, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);

        var query = _db.Devices
            .Include(d => d.Room)
            .Include(d => d.DeviceControllers).ThenInclude(dc => dc.ControllerType)
            .Include(d => d.Screens)
            .Where(d => d.BranchId == branchId);

        if (roomId.HasValue)
            query = query.Where(d => d.RoomId == roomId.Value);

        var devices = await query.OrderBy(d => d.Name).ToListAsync(ct);
        return devices.Select(d => MapDevice(d, liveStatuses)).ToList();
    }

    public async Task<DeviceDto?> GetDeviceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);

        var device = await _db.Devices
            .Include(d => d.Room)
            .Include(d => d.DeviceControllers).ThenInclude(dc => dc.ControllerType)
            .Include(d => d.Screens)
            .FirstOrDefaultAsync(d => d.Id == id && d.BranchId == branchId, ct);

        return device is null ? null : MapDevice(device, liveStatuses);
    }

    public async Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        Room? room = null;
        if (request.RoomId.HasValue)
        {
            room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId.Value && r.BranchId == branchId, ct)
                ?? throw new KeyNotFoundException("Room not found.");
        }

        if (await _db.Devices.AnyAsync(d => d.BranchId == branchId && d.Identifier == request.Identifier.Trim(), ct))
            throw new InvalidOperationException("A device with this identifier already exists in this branch.");

        var device = new Device
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            RoomId = room?.Id,
            Identifier = request.Identifier.Trim(),
            Name = request.Name.Trim()
        };

        _db.Devices.Add(device);
        await ApplyControllersAsync(device, request.Controllers, ct);
        await ApplyScreenAsync(device, request.Screen, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.Created", "Device", device.Id, new { device.Identifier, DeviceName = device.Name, RoomName = room?.Name }, ct: ct);

        await _db.Entry(device).Reference(d => d.Room).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.DeviceControllers).Query().Include(dc => dc.ControllerType).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.Screens).LoadAsync(ct);

        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);
        return MapDevice(device, liveStatuses);
    }

    public async Task<DeviceDto> UpdateDeviceAsync(Guid id, UpdateDeviceRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var device = await _db.Devices
            .Include(d => d.Room)
            .Include(d => d.DeviceControllers)
            .Include(d => d.Screens)
            .FirstOrDefaultAsync(d => d.Id == id && d.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        Room? room = null;
        if (request.RoomId.HasValue)
        {
            room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId.Value && r.BranchId == branchId, ct)
                ?? throw new KeyNotFoundException("Room not found.");
        }

        if (await _db.Devices.AnyAsync(d => d.BranchId == branchId && d.Identifier == request.Identifier.Trim() && d.Id != id, ct))
            throw new InvalidOperationException("A device with this identifier already exists in this branch.");

        device.RoomId = room?.Id;
        device.Identifier = request.Identifier.Trim();
        device.Name = request.Name.Trim();
        device.IsActive = request.IsActive;

        _db.DeviceControllers.RemoveRange(device.DeviceControllers);
        device.DeviceControllers.Clear();
        _db.Screens.RemoveRange(device.Screens);
        device.Screens.Clear();

        await ApplyControllersAsync(device, request.Controllers, ct);
        await ApplyScreenAsync(device, request.Screen, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.Updated", "Device", device.Id, new { device.Identifier, device.IsActive }, ct: ct);

        await _db.Entry(device).Reference(d => d.Room).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.DeviceControllers).Query().Include(dc => dc.ControllerType).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.Screens).LoadAsync(ct);

        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);
        return MapDevice(device, liveStatuses);
    }

    public async Task<AssetDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new KeyNotFoundException("Branch not found.");

        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);

        var rooms = await _db.Rooms
            .Include(r => r.Devices.Where(d => d.IsActive))
                .ThenInclude(d => d.DeviceControllers)
            .Include(r => r.Devices.Where(d => d.IsActive))
                .ThenInclude(d => d.Screens)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .Where(r => r.BranchId == branchId && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var roomDtos = rooms.Select(r => new AssetDashboardRoomDto(
            r.Id,
            r.Name,
            r.RoomNumber,
            r.MaxWatchingCapacity,
            r.RoomAssets.Select(MapRoomAsset).ToList(),
            r.Devices.Select(d => MapDashboardDevice(d, r.MaxWatchingCapacity, liveStatuses)).ToList()
        )).ToList();

        var unassigned = await _db.Devices
            .Include(d => d.DeviceControllers).ThenInclude(c => c.ControllerType)
            .Include(d => d.Screens)
            .Where(d => d.BranchId == branchId && d.IsActive && d.RoomId == null)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        var unassignedDtos = unassigned
            .Select(d => MapDashboardDevice(
                d,
                Math.Max(d.Screens.Sum(s => s.WorkingCount), 10),
                liveStatuses))
            .ToList();

        return new AssetDashboardDto(branchId, branch.Name, roomDtos, unassignedDtos);
    }

    private async Task ReplaceRoomAssetsAsync(Room room, IReadOnlyList<UpsertRoomAssetRequest> assets, CancellationToken ct)
    {
        _db.RoomAssets.RemoveRange(room.RoomAssets);
        room.RoomAssets.Clear();

        foreach (var a in assets)
        {
            if (a.Quantity < 0 || a.WorkingCount < 0)
                throw new InvalidOperationException("Asset quantities cannot be negative.");
            if (a.WorkingCount > a.Quantity)
                throw new InvalidOperationException("Working count cannot exceed quantity.");

            var typeExists = await _db.VenueAssetTypes.AnyAsync(t => t.Id == a.VenueAssetTypeId && t.IsActive, ct);
            if (!typeExists)
                throw new KeyNotFoundException($"Venue asset type {a.VenueAssetTypeId} not found.");

            room.RoomAssets.Add(new RoomAsset
            {
                RoomId = room.Id,
                VenueAssetTypeId = a.VenueAssetTypeId,
                Quantity = a.Quantity,
                WorkingCount = a.WorkingCount,
                Notes = a.Notes?.Trim()
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyControllersAsync(Device device, IReadOnlyList<UpsertDeviceControllerRequest>? controllers, CancellationToken ct)
    {
        if (controllers is null || controllers.Count == 0) return;

        foreach (var c in controllers)
        {
            if (c.WorkingCount > c.Quantity)
                throw new InvalidOperationException("Working controller count cannot exceed total quantity.");

            var typeExists = await _db.ControllerTypes.AnyAsync(t => t.Id == c.ControllerTypeId && t.IsActive, ct);
            if (!typeExists)
                throw new KeyNotFoundException($"Controller type {c.ControllerTypeId} not found.");

            device.DeviceControllers.Add(new DeviceController
            {
                ControllerTypeId = c.ControllerTypeId,
                Quantity = c.Quantity,
                WorkingCount = c.WorkingCount
            });
        }
    }

    private async Task ApplyScreenAsync(Device device, UpsertScreenRequest? screen, CancellationToken ct)
    {
        if (screen is null) return;

        if (screen.WorkingCount > screen.Count)
            throw new InvalidOperationException("Working screen count cannot exceed total count.");

        device.Screens.Add(new Screen
        {
            Count = screen.Count,
            WorkingCount = screen.WorkingCount,
            Notes = screen.Notes?.Trim()
        });

        await Task.CompletedTask;
    }

    private async Task<Dictionary<Guid, string>> GetLiveStatusMapAsync(Guid branchId, CancellationToken ct)
    {
        var openSessions = await _db.Sessions
            .Where(s => s.BranchId == branchId && s.Status != SessionStatus.Closed)
            .Select(s => new { s.DeviceId, s.SessionMode, s.Status })
            .ToListAsync(ct);

        return openSessions.ToDictionary(
            s => s.DeviceId,
            s => s.Status == SessionStatus.Paused
                ? "Paused"
                : s.SessionMode == SessionMode.Gaming ? "Gaming" : "Watching");
    }

    private static RoomDto MapRoom(Room room) =>
        new(room.Id, room.BranchId, room.Name, room.RoomNumber, room.MaxWatchingCapacity,
            room.VipSurchargePerHour,
            room.IsActive, room.Devices.Count(d => d.IsActive),
            room.RoomAssets.Select(MapRoomAsset).ToList(),
            room.CreatedAt);

    private static RoomAssetDto MapRoomAsset(RoomAsset a) =>
        new(a.Id, a.VenueAssetTypeId, a.VenueAssetType.Name, a.Quantity, a.WorkingCount, a.Notes);

    private static DeviceDto MapDevice(Device device, Dictionary<Guid, string> liveStatuses)
    {
        var maxPlayers = device.DeviceControllers.Sum(c => c.WorkingCount);
        var liveStatus = liveStatuses.TryGetValue(device.Id, out var status) ? status : "Idle";
        var maxWatching = device.Room?.MaxWatchingCapacity
            ?? Math.Max(device.Screens.Sum(s => s.WorkingCount), 10);

        return new DeviceDto(
            device.Id,
            device.BranchId,
            device.RoomId,
            device.Room?.Name,
            device.Identifier,
            device.Name,
            device.IsActive,
            maxPlayers,
            maxWatching,
            liveStatus,
            device.DeviceControllers.Select(c => new DeviceControllerDto(
                c.Id, c.ControllerTypeId, c.ControllerType.Name, c.Quantity, c.WorkingCount)).ToList(),
            device.Screens.Select(s => new ScreenDto(s.Id, s.Count, s.WorkingCount, s.Notes)).FirstOrDefault(),
            device.CreatedAt);
    }

    private static AssetDashboardDeviceDto MapDashboardDevice(Device device, int maxWatchingCapacity, Dictionary<Guid, string> liveStatuses)
    {
        var totalControllers = device.DeviceControllers.Sum(c => c.Quantity);
        var workingControllers = device.DeviceControllers.Sum(c => c.WorkingCount);
        var liveStatus = liveStatuses.TryGetValue(device.Id, out var status) ? status : "Idle";

        return new AssetDashboardDeviceDto(
            device.Id,
            device.Identifier,
            device.Name,
            liveStatus,
            workingControllers,
            maxWatchingCapacity,
            totalControllers,
            workingControllers,
            device.IsActive);
    }
}
