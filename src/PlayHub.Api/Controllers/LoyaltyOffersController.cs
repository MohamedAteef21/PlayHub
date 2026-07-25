using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Loyalty;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/loyalty-offers")]
[Authorize]
public class LoyaltyOffersController : ControllerBase
{
    private readonly ILoyaltyOfferService _loyalty;

    public LoyaltyOffersController(ILoyaltyOfferService loyalty) => _loyalty = loyalty;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly, CancellationToken ct) =>
        Ok(await _loyalty.GetAllAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var offer = await _loyalty.GetByIdAsync(id, ct);
        return offer is null ? NotFound() : Ok(offer);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    public async Task<IActionResult> Create([FromBody] CreateLoyaltyOfferRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _loyalty.CreateAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLoyaltyOfferRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _loyalty.UpdateAsync(id, request, ct));
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

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _loyalty.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("~/api/customers/{customerId:guid}/loyalty-credits")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    public async Task<IActionResult> GetCustomerCredits(
        Guid customerId,
        [FromQuery] bool availableOnly = true,
        CancellationToken ct = default) =>
        Ok(await _loyalty.GetCustomerCreditsAsync(customerId, availableOnly, ct));

    [HttpPost("~/api/loyalty-credits/redeem")]
    [Authorize(Policy = PermissionPolicies.SessionsClose)]
    public async Task<IActionResult> Redeem([FromBody] RedeemLoyaltyCreditRequest request, CancellationToken ct)
    {
        try
        {
            await _loyalty.RedeemCreditAsync(request, ct);
            return Ok(new { message = "Loyalty credit applied." });
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
}
