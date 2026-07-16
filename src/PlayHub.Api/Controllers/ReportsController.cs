using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Reports;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = PermissionPolicies.ReportsView)]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService) => _reportsService = reportsService;

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? branchId, CancellationToken ct) =>
        await ExecuteAsync(() => _reportsService.GetRevenueReportAsync(from, to, branchId, ct));

    [HttpGet("best-sellers")]
    public async Task<IActionResult> BestSellers(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct,
        [FromQuery] int top = 10) =>
        await ExecuteAsync(() => _reportsService.GetBestSellersAsync(from, to, top, ct));

    [HttpGet("cash-drawer")]
    public async Task<IActionResult> CashDrawer(
        [FromQuery] DateOnly date,
        [FromQuery] int tzOffsetMinutes,
        [FromQuery] Guid? branchId,
        CancellationToken ct) =>
        await ExecuteAsync(() => _reportsService.GetCashDrawerAsync(date, tzOffsetMinutes, branchId, ct));

    [HttpPost("cash-drawer/collect")]
    public async Task<IActionResult> CollectCash([FromBody] CollectCashRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _reportsService.CollectCashAsync(request, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("device-usage")]
    public async Task<IActionResult> DeviceUsage([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) =>
        await ExecuteAsync(() => _reportsService.GetDeviceUsageAsync(from, to, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
