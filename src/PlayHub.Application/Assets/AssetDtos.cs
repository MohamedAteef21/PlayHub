namespace PlayHub.Application.Assets;

public record RoomAssetDto(
    Guid Id,
    Guid VenueAssetTypeId,
    string AssetTypeName,
    int Quantity,
    int WorkingCount,
    string? Notes);

public record UpsertRoomAssetRequest(
    Guid VenueAssetTypeId,
    int Quantity,
    int WorkingCount,
    string? Notes = null);

public record RoomDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? RoomNumber,
    int MaxWatchingCapacity,
    decimal VipSurchargePerHour,
    bool IsActive,
    int DeviceCount,
    IReadOnlyList<RoomAssetDto> Assets,
    DateTime CreatedAt);

public record CreateRoomRequest(
    string Name,
    string? RoomNumber,
    int MaxWatchingCapacity,
    IReadOnlyList<UpsertRoomAssetRequest>? Assets = null,
    decimal VipSurchargePerHour = 0);

public record UpdateRoomRequest(
    string Name,
    string? RoomNumber,
    int MaxWatchingCapacity,
    bool IsActive,
    IReadOnlyList<UpsertRoomAssetRequest>? Assets = null,
    decimal VipSurchargePerHour = 0);

public record VenueAssetTypeDto(Guid Id, string Name, string? Description, bool IsActive);

public record CreateVenueAssetTypeRequest(string Name, string? Description);

public record UpdateVenueAssetTypeRequest(string Name, string? Description, bool IsActive);

public record ControllerTypeDto(Guid Id, string Name, string? Description, bool IsActive);

public record CreateControllerTypeRequest(string Name, string? Description);

public record UpdateControllerTypeRequest(string Name, string? Description, bool IsActive);

public record DeviceControllerDto(Guid Id, Guid ControllerTypeId, string ControllerTypeName, int Quantity, int WorkingCount);

public record UpsertDeviceControllerRequest(Guid ControllerTypeId, int Quantity, int WorkingCount);

public record ScreenDto(Guid Id, int Count, int WorkingCount, string? Notes);

public record UpsertScreenRequest(int Count, int WorkingCount, string? Notes);

public record DeviceDto(
    Guid Id,
    Guid BranchId,
    Guid RoomId,
    string RoomName,
    string Identifier,
    string Name,
    bool IsActive,
    int MaxGamingPlayers,
    int MaxWatchingCapacity,
    string LiveStatus,
    IReadOnlyList<DeviceControllerDto> Controllers,
    ScreenDto? Screen,
    DateTime CreatedAt);

public record CreateDeviceRequest(
    Guid RoomId,
    string Identifier,
    string Name,
    IReadOnlyList<UpsertDeviceControllerRequest>? Controllers,
    UpsertScreenRequest? Screen);

public record UpdateDeviceRequest(
    Guid RoomId,
    string Identifier,
    string Name,
    bool IsActive,
    IReadOnlyList<UpsertDeviceControllerRequest>? Controllers,
    UpsertScreenRequest? Screen);

public record AssetDashboardRoomDto(
    Guid Id,
    string Name,
    string? RoomNumber,
    int MaxWatchingCapacity,
    IReadOnlyList<RoomAssetDto> Assets,
    IReadOnlyList<AssetDashboardDeviceDto> Devices);

public record AssetDashboardDeviceDto(
    Guid Id,
    string Identifier,
    string Name,
    string LiveStatus,
    int MaxGamingPlayers,
    int MaxWatchingCapacity,
    int TotalControllers,
    int WorkingControllers,
    bool IsActive);

public record AssetDashboardDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<AssetDashboardRoomDto> Rooms,
    IReadOnlyList<DeviceDto> UnassignedDevices);
