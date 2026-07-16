using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Accounting;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/accounting")]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;

    public AccountingController(IAccountingService accountingService) => _accountingService = accountingService;

    [HttpGet("categories")]
    [Authorize(Policy = PermissionPolicies.ExpensesView)]
    public async Task<IActionResult> GetCategories(CancellationToken ct) =>
        Ok(await _accountingService.GetCategoriesAsync(ct));

    [HttpPost("categories")]
    [Authorize(Policy = PermissionPolicies.ExpensesAdd)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateExpenseCategoryRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _accountingService.CreateCategoryAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateExpenseCategoryRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _accountingService.UpdateCategoryAsync(id, request, ct));

    [HttpGet("expenses")]
    [Authorize(Policy = PermissionPolicies.ExpensesView)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await _accountingService.GetExpensesAsync(from, to, page, pageSize, ct));

    [HttpPost("expenses")]
    [Authorize(Policy = PermissionPolicies.ExpensesAdd)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _accountingService.CreateExpenseAsync(request, ct), StatusCodes.Status201Created);

    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? branchId,
        CancellationToken ct) =>
        await ExecuteAsync(() => _accountingService.GetDashboardAsync(from, to, branchId, ct));

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
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
