using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Receivables;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/receivables")]
[Authorize]
public class ReceivablesController : ControllerBase
{
    private readonly IReceivableService _receivableService;

    public ReceivablesController(IReceivableService receivableService) => _receivableService = receivableService;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? customerId, CancellationToken ct)
    {
        try
        {
            return Ok(await _receivableService.GetAllAsync(customerId, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        try
        {
            return Ok(await _receivableService.GetSummaryAsync(ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{paymentId:guid}/collect")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    public async Task<IActionResult> Collect(Guid paymentId, [FromBody] CollectReceivableRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _receivableService.CollectAsync(paymentId, request, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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
