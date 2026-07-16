using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Offers;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
public class OffersController : ControllerBase
{
    private readonly IOfferService _offers;

    public OffersController(IOfferService offers) => _offers = offers;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(IReadOnlyList<OfferDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly, CancellationToken ct) =>
        await ExecuteAsync(() => _offers.GetAllAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(OfferDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var offer = await _offers.GetByIdAsync(id, ct);
        return offer is null ? NotFound() : Ok(offer);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    [ProducesResponseType(typeof(OfferDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _offers.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    [ProducesResponseType(typeof(OfferDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _offers.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.OffersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _offers.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

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
