using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Assets;
using PlayHub.Application.Common;
using PlayHub.Domain.Common;
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

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var resolvedBranchId = await ResolveReadableBranchIdAsync(branchId, ct);
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);

        var rooms = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .Where(r => r.BranchId == resolvedBranchId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return rooms.Select(r => MapRoom(r, ownerFilter)).ToList();
    }

    public async Task<RoomDto?> GetRoomByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);

        var room = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct);

        return room is null ? null : MapRoom(room, ownerFilter);
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var room = new Room
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            VipSurchargePerHour = 0,
            Name = request.Name.Trim(),
            RoomNumber = request.RoomNumber?.Trim(),
            MaxWatchingCapacity = request.MaxWatchingCapacity,
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);

        if (request.Assets is { Count: > 0 })
            await ReplaceRoomAssetsAsync(room, request.Assets, ct);

        await _audit.LogAsync("Room.Created", "Room", room.Id, new { room.Name, room.MaxWatchingCapacity }, ct: ct);

        await _db.Entry(room).Collection(r => r.RoomAssets).Query()
            .Include(a => a.VenueAssetType).LoadAsync(ct);
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        return MapRoom(room, ownerFilter);
    }

    public async Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var room = await _db.Rooms
            .Include(r => r.Devices)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        room.Name = request.Name.Trim();
        room.RoomNumber = request.RoomNumber?.Trim();
        room.MaxWatchingCapacity = request.MaxWatchingCapacity;
        room.IsActive = request.IsActive;

        if (request.Assets is not null)
            await ReplaceRoomAssetsAsync(room, request.Assets, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Room.Updated", "Room", room.Id, new { room.Name, room.IsActive }, ct: ct);

        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        return MapRoom(room, ownerFilter);
    }

    public async Task SoftDeleteRoomAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var room = await _db.Rooms
            .Include(r => r.Devices)
            .FirstOrDefaultAsync(r => r.Id == id && r.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        var deviceIds = room.Devices.Where(d => !d.IsDeleted).Select(d => d.Id).ToList();
        if (deviceIds.Count > 0 &&
            await _db.Sessions.AnyAsync(s => deviceIds.Contains(s.DeviceId) && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("Cannot delete a room that has devices with active sessions.");

        foreach (var device in room.Devices.Where(d => !d.IsDeleted))
            device.RoomId = null;

        room.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        room.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Room.SoftDeleted", "Room", room.Id, new { room.Name }, ct: ct);
    }

    public async Task<IReadOnlyList<VenueAssetTypeDto>> GetVenueAssetTypesAsync(CancellationToken ct = default)
    {
        var query = _db.VenueAssetTypes.AsQueryable();
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue)
            query = query.Where(c => c.OwnerUserId == ownerFilter.Value);

        var types = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var typeIds = types.Select(t => t.Id).ToList();

        // Only count assignments on rooms belonging to this owner's branches (avoid cross-master stock).
        var ownerBranchIds = ownerFilter.HasValue
            ? await _db.Branches.Where(b => b.OwnerUserId == ownerFilter.Value).Select(b => b.Id).ToListAsync(ct)
            : null;

        var assignedQuery = _db.RoomAssets.Where(a => typeIds.Contains(a.VenueAssetTypeId));
        if (ownerBranchIds is not null)
            assignedQuery = assignedQuery.Where(a => ownerBranchIds.Contains(a.Room.BranchId));

        var assigned = await assignedQuery
            .GroupBy(a => a.VenueAssetTypeId)
            .Select(g => new { TypeId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.TypeId, x => x.Qty, ct);

        return types.Select(c => new VenueAssetTypeDto(
            c.Id,
            c.Name,
            c.Description,
            c.IsActive,
            c.TotalQuantity,
            c.WorkingCount,
            assigned.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<VenueAssetTypeDto> CreateVenueAssetTypeAsync(CreateVenueAssetTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Asset type name is required.");
        if (request.TotalQuantity < 0 || request.WorkingCount < 0)
            throw new InvalidOperationException("Asset quantities cannot be negative.");
        if (request.WorkingCount > request.TotalQuantity)
            throw new InvalidOperationException("Working count cannot exceed total quantity.");

        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        var type = new VenueAssetType
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
            Name = name,
            Description = request.Description?.Trim(),
            TotalQuantity = request.TotalQuantity,
            WorkingCount = request.WorkingCount
        };

        _db.VenueAssetTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("VenueAssetType.Created", "VenueAssetType", type.Id, new { type.Name, type.TotalQuantity }, ct: ct);

        return new VenueAssetTypeDto(type.Id, type.Name, type.Description, type.IsActive, type.TotalQuantity, type.WorkingCount, 0);
    }

    public async Task<VenueAssetTypeDto> UpdateVenueAssetTypeAsync(Guid id, UpdateVenueAssetTypeRequest request, CancellationToken ct = default)
    {
        var type = await RequireOwnedVenueAssetTypeAsync(id, ct);

        if (request.TotalQuantity < 0 || request.WorkingCount < 0)
            throw new InvalidOperationException("Asset quantities cannot be negative.");
        if (request.WorkingCount > request.TotalQuantity)
            throw new InvalidOperationException("Working count cannot exceed total quantity.");

        var assigned = await _db.RoomAssets.Where(a => a.VenueAssetTypeId == id).SumAsync(a => (int?)a.Quantity, ct) ?? 0;
        if (request.TotalQuantity < assigned)
            throw new InvalidOperationException(
                $"Total quantity ({request.TotalQuantity}) cannot be less than already assigned to rooms ({assigned}).");

        type.Name = request.Name.Trim();
        type.Description = request.Description?.Trim();
        type.IsActive = request.IsActive;
        type.TotalQuantity = request.TotalQuantity;
        type.WorkingCount = request.WorkingCount;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("VenueAssetType.Updated", "VenueAssetType", type.Id, new { type.Name, type.TotalQuantity, type.IsActive }, ct: ct);

        return new VenueAssetTypeDto(type.Id, type.Name, type.Description, type.IsActive, type.TotalQuantity, type.WorkingCount, assigned);
    }

    public async Task SoftDeleteVenueAssetTypeAsync(Guid id, CancellationToken ct = default)
    {
        var type = await RequireOwnedVenueAssetTypeAsync(id, ct);

        // Drop room assignments so rooms don't keep showing a deleted catalog item.
        var assigned = await _db.RoomAssets.Where(a => a.VenueAssetTypeId == id).ToListAsync(ct);
        if (assigned.Count > 0)
            _db.RoomAssets.RemoveRange(assigned);

        type.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        type.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("VenueAssetType.SoftDeleted", "VenueAssetType", type.Id, new { type.Name }, ct: ct);
    }

    public async Task<IReadOnlyList<ControllerTypeDto>> GetControllerTypesAsync(CancellationToken ct = default)
    {
        var query = _db.ControllerTypes.AsQueryable();
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue)
            query = query.Where(c => c.OwnerUserId == ownerFilter.Value);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new ControllerTypeDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ControllerTypeDto> CreateControllerTypeAsync(CreateControllerTypeRequest request, CancellationToken ct = default)
    {
        var ownerId = await OwnerScope.ResolveCatalogOwnerIdAsync(_db, _tenantContext, ct);
        var type = new ControllerType
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
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
        var type = await RequireOwnedControllerTypeAsync(id, ct);

        type.Name = request.Name.Trim();
        type.Description = request.Description?.Trim();
        type.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ControllerType.Updated", "ControllerType", type.Id, new { type.Name, type.IsActive }, ct: ct);

        return new ControllerTypeDto(type.Id, type.Name, type.Description, type.IsActive);
    }

    public async Task SoftDeleteControllerTypeAsync(Guid id, CancellationToken ct = default)
    {
        var type = await RequireOwnedControllerTypeAsync(id, ct);

        if (await _db.DeviceControllers.AnyAsync(c => c.ControllerTypeId == id, ct))
            throw new InvalidOperationException(
                "Cannot delete a controller type that is assigned to devices. Remove it from devices first.");

        type.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        type.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ControllerType.SoftDeleted", "ControllerType", type.Id, new { type.Name }, ct: ct);
    }

    private async Task<VenueAssetType> RequireOwnedVenueAssetTypeAsync(Guid id, CancellationToken ct)
    {
        var type = await _db.VenueAssetTypes.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Venue asset type not found.");

        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue && !OwnerScope.CanAccess(type.OwnerUserId, ownerFilter.Value, false))
            throw new KeyNotFoundException("Venue asset type not found.");

        return type;
    }

    private async Task<ControllerType> RequireOwnedControllerTypeAsync(Guid id, CancellationToken ct)
    {
        var type = await _db.ControllerTypes.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Controller type not found.");

        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue && !OwnerScope.CanAccess(type.OwnerUserId, ownerFilter.Value, false))
            throw new KeyNotFoundException("Controller type not found.");

        return type;
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(Guid? roomId = null, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
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
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
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
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        Room? room = null;
        if (request.RoomId.HasValue)
        {
            room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId.Value && r.BranchId == branchId, ct)
                ?? throw new KeyNotFoundException("Room not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Device name is required.");

        // Identifier is internal — default to the display name so users only enter Name.
        var identifier = string.IsNullOrWhiteSpace(request.Identifier) ? name : request.Identifier.Trim();

        if (await _db.Devices.AnyAsync(d => d.BranchId == branchId && d.Identifier == identifier, ct))
            throw new InvalidOperationException("A device with this name already exists in this branch.");

        var device = new Device
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            RoomId = room?.Id,
            Identifier = identifier,
            Name = name
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
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

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

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Device name is required.");

        var identifier = string.IsNullOrWhiteSpace(request.Identifier) ? name : request.Identifier.Trim();

        if (await _db.Devices.AnyAsync(d => d.BranchId == branchId && d.Identifier == identifier && d.Id != id, ct))
            throw new InvalidOperationException("A device with this name already exists in this branch.");

        device.RoomId = room?.Id;
        device.Identifier = identifier;
        device.Name = name;
        device.IsActive = request.IsActive;

        if (request.Controllers is not null)
        {
            _db.DeviceControllers.RemoveRange(device.DeviceControllers);
            device.DeviceControllers.Clear();
            await ApplyControllersAsync(device, request.Controllers, ct);
        }

        if (request.Screen is not null)
        {
            _db.Screens.RemoveRange(device.Screens);
            device.Screens.Clear();
            await ApplyScreenAsync(device, request.Screen, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.Updated", "Device", device.Id, new { device.Identifier, device.IsActive }, ct: ct);

        await _db.Entry(device).Reference(d => d.Room).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.DeviceControllers).Query().Include(dc => dc.ControllerType).LoadAsync(ct);
        await _db.Entry(device).Collection(d => d.Screens).LoadAsync(ct);

        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);
        return MapDevice(device, liveStatuses);
    }

    public async Task SoftDeleteDeviceAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        if (await _db.Sessions.AnyAsync(s => s.DeviceId == id && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("Cannot delete a device with an active session. Close the session first.");

        device.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        device.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.SoftDeleted", "Device", device.Id, new { device.Identifier, device.Name }, ct: ct);
    }

    public async Task<DeviceDto> MoveDeviceAsync(Guid id, MoveDeviceRequest request, CancellationToken ct = default)
    {
        if (!_tenantContext.IsMaster && !_tenantContext.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only Master Admin can move devices between branches.");

        var sourceBranchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var targetBranchId = request.TargetBranchId;
        if (targetBranchId == Guid.Empty)
            throw new InvalidOperationException("Target branch is required.");

        if (targetBranchId == sourceBranchId)
            throw new InvalidOperationException("Device is already on this branch. Pick a different branch.");

        await EnsureOwnedBranchAsync(targetBranchId, ct);

        var device = await _db.Devices
            .Include(d => d.Room)
            .Include(d => d.DeviceControllers).ThenInclude(dc => dc.ControllerType)
            .Include(d => d.Screens)
            .FirstOrDefaultAsync(d => d.Id == id && d.BranchId == sourceBranchId, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        if (await _db.Sessions.AnyAsync(s => s.DeviceId == id && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("Cannot move a device with an active session. Close the session first.");

        if (await _db.DeviceMaintenances.AnyAsync(m => m.DeviceId == id && m.CompletedAt == null, ct))
            throw new InvalidOperationException("Cannot move a device that is in maintenance. Complete maintenance first.");

        Room? targetRoom = null;
        if (request.TargetRoomId.HasValue)
        {
            targetRoom = await _db.Rooms.FirstOrDefaultAsync(
                r => r.Id == request.TargetRoomId.Value && r.BranchId == targetBranchId && r.IsActive, ct)
                ?? throw new KeyNotFoundException("Target room not found on the destination branch.");
        }

        if (await _db.Devices.AnyAsync(
                d => d.BranchId == targetBranchId && d.Identifier == device.Identifier && d.Id != device.Id, ct))
            throw new InvalidOperationException(
                "A device with the same name already exists on the destination branch. Rename it first.");

        var fromBranchId = device.BranchId;
        var fromRoomId = device.RoomId;
        device.BranchId = targetBranchId;
        device.RoomId = targetRoom?.Id;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Device.Moved", "Device", device.Id, new
        {
            device.Identifier,
            device.Name,
            FromBranchId = fromBranchId,
            ToBranchId = targetBranchId,
            FromRoomId = fromRoomId,
            ToRoomId = device.RoomId,
            TargetRoomName = targetRoom?.Name
        }, ct: ct);

        await _db.Entry(device).Reference(d => d.Room).LoadAsync(ct);
        var liveStatuses = await GetLiveStatusMapAsync(targetBranchId, ct);
        return MapDevice(device, liveStatuses);
    }

    /// <summary>
    /// Active branch by default. Masters may pass another owned branch id (e.g. rooms for a move target).
    /// </summary>
    private async Task<Guid> ResolveReadableBranchIdAsync(Guid? branchId, CancellationToken ct)
    {
        if (branchId is null || branchId == Guid.Empty)
            return await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        if (!_tenantContext.IsMaster && !_tenantContext.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only Master Admin can read another branch.");

        await EnsureOwnedBranchAsync(branchId.Value, ct);
        return branchId.Value;
    }

    private async Task EnsureOwnedBranchAsync(Guid branchId, CancellationToken ct)
    {
        if (!_tenantContext.IsSuperAdmin
            && _tenantContext.AllowedBranchIds.Count > 0
            && !_tenantContext.AllowedBranchIds.Contains(branchId))
        {
            throw new UnauthorizedAccessException("You do not have access to this branch.");
        }

        var branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new KeyNotFoundException("Branch not found.");

        if (_tenantContext.IsSuperAdmin)
            return;

        var businessOwnerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        if (branch.OwnerUserId.HasValue && branch.OwnerUserId.Value != businessOwnerId)
            throw new UnauthorizedAccessException("You do not have access to this branch.");
    }

    public async Task<AssetDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new KeyNotFoundException("Branch not found.");

        var liveStatuses = await GetLiveStatusMapAsync(branchId, ct);

        var rooms = await _db.Rooms
            .Include(r => r.Devices)
                .ThenInclude(d => d.DeviceControllers)
            .Include(r => r.Devices)
                .ThenInclude(d => d.Screens)
            .Include(r => r.RoomAssets).ThenInclude(a => a.VenueAssetType)
            .Where(r => r.BranchId == branchId && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);

        var roomDtos = rooms.Select(r => new AssetDashboardRoomDto(
            r.Id,
            r.Name,
            r.RoomNumber,
            r.MaxWatchingCapacity,
            r.RoomAssets
                .Where(a => a.VenueAssetType is { IsDeleted: false }
                    && (!ownerFilter.HasValue || a.VenueAssetType.OwnerUserId == ownerFilter.Value))
                .Select(MapRoomAsset)
                .ToList(),
            r.Devices.OrderBy(d => d.Name).Select(d => MapDashboardDevice(d, r.MaxWatchingCapacity, liveStatuses)).ToList()
        )).ToList();

        var unassigned = await _db.Devices
            .Include(d => d.DeviceControllers).ThenInclude(c => c.ControllerType)
            .Include(d => d.Screens)
            .Where(d => d.BranchId == branchId && d.RoomId == null)
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
        // Validate stock before mutating: assigned elsewhere + this room ≤ TotalQuantity.
        foreach (var group in assets.GroupBy(a => a.VenueAssetTypeId))
        {
            var typeId = group.Key;
            var requestQty = group.Sum(a => a.Quantity);
            var type = await RequireOwnedVenueAssetTypeAsync(typeId, ct);
            if (!type.IsActive)
                throw new KeyNotFoundException($"Venue asset type {typeId} not found.");

            var assignedElsewhere = await _db.RoomAssets
                .Where(a => a.VenueAssetTypeId == typeId && a.RoomId != room.Id
                    && a.Room.Branch.OwnerUserId == type.OwnerUserId)
                .SumAsync(a => (int?)a.Quantity, ct) ?? 0;

            if (assignedElsewhere + requestQty > type.TotalQuantity)
            {
                var available = Math.Max(0, type.TotalQuantity - assignedElsewhere);
                throw new InvalidOperationException(
                    $"Not enough '{type.Name}' stock. Available to assign: {available}, requested: {requestQty}. Add stock in Venue Assets first.");
            }
        }

        _db.RoomAssets.RemoveRange(room.RoomAssets);
        room.RoomAssets.Clear();

        foreach (var a in assets)
        {
            if (a.Quantity < 0 || a.WorkingCount < 0)
                throw new InvalidOperationException("Asset quantities cannot be negative.");
            if (a.WorkingCount > a.Quantity)
                throw new InvalidOperationException("Working count cannot exceed quantity.");

            await RequireOwnedVenueAssetTypeAsync(a.VenueAssetTypeId, ct);

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

    private static RoomDto MapRoom(Room room, Guid? ownerFilter = null) =>
        new(room.Id, room.BranchId, room.Name, room.RoomNumber, room.MaxWatchingCapacity,
            room.IsActive, room.Devices.Count(d => d.IsActive && !d.IsDeleted),
            room.RoomAssets
                .Where(a => a.VenueAssetType is { IsDeleted: false }
                    && (!ownerFilter.HasValue || a.VenueAssetType.OwnerUserId == ownerFilter.Value))
                .Select(MapRoomAsset)
                .ToList(),
            room.CreatedAt);

    private static RoomAssetDto MapRoomAsset(RoomAsset a) =>
        new(a.Id, a.VenueAssetTypeId, a.VenueAssetType?.Name ?? "—", a.Quantity, a.WorkingCount, a.Notes);

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
