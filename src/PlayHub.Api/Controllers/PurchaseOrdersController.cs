using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.PurchaseOrders;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService) =>
        _purchaseOrderService = purchaseOrderService;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await _purchaseOrderService.GetAllAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InventoryView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _purchaseOrderService.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersCreate)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _purchaseOrderService.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("{id:guid}/order")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersCreate)]
    public async Task<IActionResult> MarkOrdered(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _purchaseOrderService.MarkOrderedAsync(id, ct));

    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = PermissionPolicies.PurchaseOrdersReceive)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _purchaseOrderService.ReceiveAsync(id, ct));

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
