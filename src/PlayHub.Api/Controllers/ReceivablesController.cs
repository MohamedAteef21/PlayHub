using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        try
        {
            return Ok(await _receivableService.GetAllAsync(ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{paymentId:guid}/collect")]
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
