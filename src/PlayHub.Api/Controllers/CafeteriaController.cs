using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Cafeteria;

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
    public async Task<IActionResult> GetItems(CancellationToken ct) =>
        Ok(await _cafeteriaService.GetItemsAsync(ct));

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

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, int successCode = StatusCodes.Status200OK)
    {
        try
        {
            var result = await action();
            return successCode == StatusCodes.Status201Created ? Created(string.Empty, result) : Ok(result);
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
