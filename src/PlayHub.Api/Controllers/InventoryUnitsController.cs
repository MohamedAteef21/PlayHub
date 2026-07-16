using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Inventory;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/inventory/units")]
[Authorize]
public class InventoryUnitsController : ControllerBase
{
    private readonly IInventoryUnitService _units;

    public InventoryUnitsController(IInventoryUnitService units) => _units = units;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(await _units.GetAllAsync(activeOnly, ct));

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> Create([FromBody] CreateInventoryUnitRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _units.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryUnitRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _units.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _units.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("~/api/inventory/conversion-logs")]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetConversionLogs(
        [FromQuery] Guid? itemId,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _units.GetConversionLogsAsync(itemId, take, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, int successStatus = StatusCodes.Status200OK)
    {
        try
        {
            var result = await action();
            return successStatus == StatusCodes.Status201Created ? Created(string.Empty, result) : Ok(result);
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
