using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Reservations;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IDeviceReservationService _reservations;

    public ReservationsController(IDeviceReservationService reservations) => _reservations = reservations;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceReservationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming(CancellationToken ct) =>
        Ok(await _reservations.GetUpcomingAsync(ct));

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    [ProducesResponseType(typeof(DeviceReservationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDeviceReservationRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _reservations.CreateAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            await _reservations.CancelAsync(id, ct);
            return Ok(new { message = "Cancelled" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("conflict")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(ReservationConflictDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckConflict([FromQuery] Guid deviceId, CancellationToken ct) =>
        Ok(await _reservations.CheckOpenConflictAsync(deviceId, ct));
}
