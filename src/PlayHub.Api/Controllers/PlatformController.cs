using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Application.Platform;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IPlatformSettingsService _platform;
    private readonly TenantContext _tenant;

    public PlatformController(IPlatformSettingsService platform, TenantContext tenant)
    {
        _platform = platform;
        _tenant = tenant;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        if (!_tenant.IsSuperAdmin) return Forbid();
        try
        {
            return Ok(await _platform.GetDashboardAsync(ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("alert-settings")]
    public async Task<IActionResult> GetAlertSettings(CancellationToken ct)
    {
        if (!_tenant.IsSuperAdmin) return Forbid();
        try
        {
            return Ok(await _platform.GetAlertSettingsAsync(ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("alert-settings")]
    public async Task<IActionResult> UpsertAlertSettings(
        [FromBody] UpsertPlatformAlertSettingsRequest request,
        CancellationToken ct)
    {
        if (!_tenant.IsSuperAdmin) return Forbid();
        try
        {
            return Ok(await _platform.UpsertAlertSettingsAsync(request, ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("alert-settings/test-email")]
    public async Task<IActionResult> TestEmail(CancellationToken ct)
    {
        if (!_tenant.IsSuperAdmin) return Forbid();
        try
        {
            await _platform.SendPlatformTestEmailAsync(ct);
            return Ok(new { message = "Test email sent" });
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
