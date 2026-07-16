using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Alerts;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertSettingsService _settings;
    private readonly IDeviceMaintenanceService _maintenance;
    private readonly IInvoicePdfService _pdf;

    public AlertsController(
        IAlertSettingsService settings,
        IDeviceMaintenanceService maintenance,
        IInvoicePdfService pdf)
    {
        _settings = settings;
        _maintenance = maintenance;
        _pdf = pdf;
    }

    [HttpGet("settings")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        try
        {
            return Ok(await _settings.GetMySettingsAsync(ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("settings")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    public async Task<IActionResult> UpsertSettings([FromBody] UpsertMasterAlertSettingsRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _settings.UpsertMySettingsAsync(request, ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("settings/test-email")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    public async Task<IActionResult> TestEmail(CancellationToken ct)
    {
        try
        {
            await _settings.SendTestEmailAsync(ct);
            return Ok(new { message = "Test email sent" });
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("maintenance")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    public async Task<IActionResult> GetOpenMaintenance(CancellationToken ct) =>
        Ok(await _maintenance.GetOpenAsync(ct));

    [HttpGet("maintenance/history")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    public async Task<IActionResult> GetMaintenanceHistory([FromQuery] int take = 50, CancellationToken ct = default) =>
        Ok(await _maintenance.GetHistoryAsync(take, ct));

    [HttpPost("maintenance")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    public async Task<IActionResult> StartMaintenance([FromBody] StartDeviceMaintenanceRequest request, CancellationToken ct)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created, await _maintenance.StartAsync(request, ct));
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

    [HttpPost("maintenance/{id:guid}/complete")]
    [Authorize(Policy = PermissionPolicies.AssetsManage)]
    public async Task<IActionResult> CompleteMaintenance(Guid id, [FromBody] CompleteDeviceMaintenanceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _maintenance.CompleteAsync(id, request, ct));
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

    [HttpGet("invoices/{sessionId:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.SessionsClose)]
    public async Task<IActionResult> DownloadInvoicePdf(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var bytes = await _pdf.BuildSessionInvoicePdfAsync(sessionId, ct);
            return File(bytes, "application/pdf", $"invoice-{sessionId:N}.pdf");
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
