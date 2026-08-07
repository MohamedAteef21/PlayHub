using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Cafeteria;
using PlayHub.Domain.Enums;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/cafeteria")]
[Authorize]
public class CafeteriaController : ControllerBase
{
    private readonly ICafeteriaService _cafeteriaService;

    public CafeteriaController(ICafeteriaService cafeteriaService) => _cafeteriaService = cafeteriaService;

    [HttpGet("items")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetItems(
        [FromQuery] CafeteriaItemKind? kind,
        [FromQuery] bool forSaleOnly = false,
        CancellationToken ct = default) =>
        Ok(await _cafeteriaService.GetItemsAsync(kind, forSaleOnly, ct));

    [HttpGet("items/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var item = await _cafeteriaService.GetItemByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("items")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> CreateItem([FromBody] CreateCafeteriaItemRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.CreateItemAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("items/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateCafeteriaItemRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.UpdateItemAsync(id, request, ct));

    [HttpDelete("items/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        try
        {
            await _cafeteriaService.SoftDeleteItemAsync(id, ct);
            return NoContent();
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

    [HttpGet("addons")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetAddOns([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await _cafeteriaService.GetAddOnsAsync(activeOnly, ct));

    [HttpPost("addons")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> CreateAddOn([FromBody] CreateCafeteriaAddOnRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.CreateAddOnAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("addons/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> UpdateAddOn(Guid id, [FromBody] UpdateCafeteriaAddOnRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.UpdateAddOnAsync(id, request, ct));

    [HttpDelete("addons/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryManageItems)]
    public async Task<IActionResult> DeleteAddOn(Guid id, CancellationToken ct)
    {
        try
        {
            await _cafeteriaService.SoftDeleteAddOnAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("sales")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetSales([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await _cafeteriaService.GetSalesAsync(from, to, ct));

    [HttpGet("sales/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetSale(Guid id, CancellationToken ct)
    {
        var sale = await _cafeteriaService.GetSaleByIdAsync(id, ct);
        return sale is null ? NotFound() : Ok(sale);
    }

    [HttpPost("sales")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    public async Task<IActionResult> CreateSale([FromBody] CreateCafeteriaSaleRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.CreateStandaloneSaleAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("sales/{id:guid}/returns")]
    [Authorize(Policy = PermissionPolicies.CafeteriaReturn)]
    public async Task<IActionResult> ReturnItem(Guid id, [FromBody] ReturnCafeteriaItemRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.ReturnItemAsync(id, request, ct));

    [HttpGet("holds")]
    [Authorize(Policy = PermissionPolicies.CafeteriaView)]
    public async Task<IActionResult> GetOpenHolds(CancellationToken ct) =>
        Ok(await _cafeteriaService.GetOpenHoldsAsync(ct));

    [HttpPost("holds")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    public async Task<IActionResult> CreateHold([FromBody] CreateCafeteriaHoldRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.CreateHoldAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("holds/{id:guid}/attach-session")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    public async Task<IActionResult> AttachHoldToSession(Guid id, [FromBody] AttachHoldToSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.AttachToSessionAsync(id, request, ct));

    [HttpPost("holds/{id:guid}/convert-sale")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    public async Task<IActionResult> ConvertHoldToSale(Guid id, [FromBody] ConvertHoldToSaleRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.ConvertToSaleAsync(id, request, ct));

    [HttpPost("holds/{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    public async Task<IActionResult> CancelHold(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _cafeteriaService.CancelHoldAsync(id, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, int successCode = StatusCodes.Status200OK)
    {
        try
        {
            var result = await action();
            return successCode == StatusCodes.Status201Created ? Created(string.Empty, result) : Ok(result);
        }
        catch (MissingIngredientsException ex)
        {
            return Conflict(new
            {
                code = MissingIngredientsException.ErrorCode,
                message = ex.Message,
                missing = ex.Missing
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
