using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Common;
using PlayHub.Application.Customers;
using PlayHub.Application.Sessions;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly ISessionService _sessions;

    public CustomersController(ICustomerService customers, ISessionService sessions)
    {
        _customers = customers;
        _sessions = sessions;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        await ExecuteAsync(() => _customers.SearchAsync(q, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("{id:guid}/sessions")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(PagedResult<SessionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(id, ct);
        if (customer is null)
            return NotFound(new { message = "Customer not found." });

        return await ExecuteAsync(() =>
            _sessions.GetSessionHistoryAsync(null, null, page, pageSize, id, ct));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _customers.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _customers.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/wallet/topup")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> TopUpWallet(Guid id, [FromBody] WalletTopUpRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _customers.TopUpWalletAsync(id, request, ct));

    [HttpGet("{id:guid}/wallet")]
    [Authorize(Policy = PermissionPolicies.CustomersView)]
    [ProducesResponseType(typeof(PagedResult<WalletTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWalletTransactions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        await ExecuteAsync(() => _customers.GetWalletTransactionsAsync(id, page, pageSize, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _customers.SoftDeleteAsync(id, ct);
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
