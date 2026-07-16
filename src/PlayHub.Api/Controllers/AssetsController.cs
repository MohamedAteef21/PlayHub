using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Assets;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;

    public AssetsController(IAssetService assetService) => _assetService = assetService;

    /// <summary>Visual dashboard — all rooms and devices with live status for the active branch.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(AssetDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.GetDashboardAsync(ct));

    // --- Rooms ---

    [HttpGet("rooms")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRooms(CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.GetRoomsAsync(ct));

    [HttpGet("rooms/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoom(Guid id, CancellationToken ct)
    {
        var room = await _assetService.GetRoomByIdAsync(id, ct);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpPost("rooms")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct) =>
        await ExecuteCreatedAsync(() => _assetService.CreateRoomAsync(request, ct), r => r.Id, nameof(GetRoom));

    [HttpPut("rooms/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.UpdateRoomAsync(id, request, ct));

    // --- Venue asset types (couches, etc.) ---

    [HttpGet("venue-asset-types")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(IReadOnlyList<VenueAssetTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenueAssetTypes(CancellationToken ct) =>
        Ok(await _assetService.GetVenueAssetTypesAsync(ct));

    [HttpPost("venue-asset-types")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(VenueAssetTypeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVenueAssetType([FromBody] CreateVenueAssetTypeRequest request, CancellationToken ct)
    {
        var result = await _assetService.CreateVenueAssetTypeAsync(request, ct);
        return CreatedAtAction(nameof(GetVenueAssetTypes), result);
    }

    [HttpPut("venue-asset-types/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(VenueAssetTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateVenueAssetType(Guid id, [FromBody] UpdateVenueAssetTypeRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.UpdateVenueAssetTypeAsync(id, request, ct));

    // --- Controller Types ---

    [HttpGet("controller-types")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ControllerTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetControllerTypes(CancellationToken ct) =>
        Ok(await _assetService.GetControllerTypesAsync(ct));

    [HttpPost("controller-types")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(ControllerTypeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateControllerType([FromBody] CreateControllerTypeRequest request, CancellationToken ct)
    {
        var result = await _assetService.CreateControllerTypeAsync(request, ct);
        return CreatedAtAction(nameof(GetControllerTypes), result);
    }

    [HttpPut("controller-types/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(ControllerTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateControllerType(Guid id, [FromBody] UpdateControllerTypeRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.UpdateControllerTypeAsync(id, request, ct));

    // --- Devices ---

    [HttpGet("devices")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices([FromQuery] Guid? roomId, CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.GetDevicesAsync(roomId, ct));

    [HttpGet("devices/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevice(Guid id, CancellationToken ct)
    {
        var device = await _assetService.GetDeviceByIdAsync(id, ct);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost("devices")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request, CancellationToken ct) =>
        await ExecuteCreatedAsync(() => _assetService.CreateDeviceAsync(request, ct), d => d.Id, nameof(GetDevice));

    [HttpPut("devices/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromBody] UpdateDeviceRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _assetService.UpdateDeviceAsync(id, request, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> ExecuteCreatedAsync<T>(Func<Task<T>> action, Func<T, Guid> idSelector, string getActionName)
    {
        try
        {
            var result = await action();
            return CreatedAtAction(getActionName, new { id = idSelector(result) }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
