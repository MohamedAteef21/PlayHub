using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Pricing;
using PlayHub.Domain.Enums;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/pricing")]
[Authorize]
public class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingController(IPricingService pricingService) => _pricingService = pricingService;

    [HttpGet("plans")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<PricingPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans([FromQuery] SessionMode? mode, CancellationToken ct) =>
        await ExecuteAsync(() => _pricingService.GetPlansAsync(mode, ct));

    [HttpGet("plans/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(PricingPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct)
    {
        var plan = await _pricingService.GetPlanByIdAsync(id, ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpPost("plans")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(PricingPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePricingPlanRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _pricingService.CreatePlanAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("plans/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(PricingPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePricingPlanRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _pricingService.UpdatePlanAsync(id, request, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, int successStatus = StatusCodes.Status200OK)
    {
        try
        {
            var result = await action();
            return successStatus switch
            {
                StatusCodes.Status201Created => Created(string.Empty, result),
                _ => Ok(result)
            };
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
