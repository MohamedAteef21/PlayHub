using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Inventory;
using PlayHub.Domain.Enums;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    [HttpGet("movements")]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] Guid? itemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await _inventoryService.GetMovementsAsync(itemId, page, pageSize, ct));

    [HttpPost("items/{id:guid}/adjust")]
    [Authorize(Policy = PermissionPolicies.InventoryAdjust)]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] AdjustInventoryRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _inventoryService.AdjustQuantityAsync(id, request, ct));

    [HttpGet("vouchers")]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetVouchers(
        [FromQuery] StockVoucherType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await _inventoryService.GetVouchersAsync(type, page, pageSize, ct));

    [HttpGet("vouchers/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetVoucher(Guid id, CancellationToken ct)
    {
        var voucher = await _inventoryService.GetVoucherAsync(id, ct);
        return voucher is null ? NotFound() : Ok(voucher);
    }

    [HttpPost("vouchers")]
    [Authorize(Policy = PermissionPolicies.InventoryAdjust)]
    public async Task<IActionResult> CreateVoucher([FromBody] CreateStockVoucherRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _inventoryService.CreateVoucherAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("vouchers/{id:guid}/post")]
    [Authorize(Policy = PermissionPolicies.InventoryAdjust)]
    public async Task<IActionResult> PostVoucher(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _inventoryService.PostVoucherAsync(id, ct));

    [HttpPost("vouchers/{countId:guid}/settlement")]
    [Authorize(Policy = PermissionPolicies.InventoryAdjust)]
    public async Task<IActionResult> SettlementFromCount(Guid countId, [FromBody] SettlementFromCountRequest? body, CancellationToken ct) =>
        await ExecuteAsync(() => _inventoryService.CreateSettlementFromCountAsync(countId, body?.Notes, ct), StatusCodes.Status201Created);

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

public record SettlementFromCountRequest(string? Notes);
