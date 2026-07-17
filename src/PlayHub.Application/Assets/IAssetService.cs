using PlayHub.Application.Assets;

namespace PlayHub.Application.Assets;

public interface IAssetService
{
    // Rooms
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default);
    Task SoftDeleteRoomAsync(Guid id, CancellationToken ct = default);

    // Venue asset types (couches, screens furniture, etc.)
    Task<IReadOnlyList<VenueAssetTypeDto>> GetVenueAssetTypesAsync(CancellationToken ct = default);
    Task<VenueAssetTypeDto> CreateVenueAssetTypeAsync(CreateVenueAssetTypeRequest request, CancellationToken ct = default);
    Task<VenueAssetTypeDto> UpdateVenueAssetTypeAsync(Guid id, UpdateVenueAssetTypeRequest request, CancellationToken ct = default);
    Task SoftDeleteVenueAssetTypeAsync(Guid id, CancellationToken ct = default);

    // Controller Types
    Task<IReadOnlyList<ControllerTypeDto>> GetControllerTypesAsync(CancellationToken ct = default);
    Task<ControllerTypeDto> CreateControllerTypeAsync(CreateControllerTypeRequest request, CancellationToken ct = default);
    Task<ControllerTypeDto> UpdateControllerTypeAsync(Guid id, UpdateControllerTypeRequest request, CancellationToken ct = default);
    Task SoftDeleteControllerTypeAsync(Guid id, CancellationToken ct = default);

    // Devices
    Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(Guid? roomId = null, CancellationToken ct = default);
    Task<DeviceDto?> GetDeviceByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken ct = default);
    Task<DeviceDto> UpdateDeviceAsync(Guid id, UpdateDeviceRequest request, CancellationToken ct = default);
    Task SoftDeleteDeviceAsync(Guid id, CancellationToken ct = default);

    // Dashboard
    Task<AssetDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
