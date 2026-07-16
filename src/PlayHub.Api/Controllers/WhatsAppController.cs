using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.WhatsApp;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;

    public WhatsAppController(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    [HttpGet("status")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(WhatsAppStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.GetStatusAsync(ct));

    [HttpGet("qr")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(WhatsAppQrDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQr(CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.GetQrAsync(ct));

    [HttpPost("session")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(WhatsAppStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveSession([FromBody] SaveWhatsAppSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.SaveSessionAsync(request, ct));

    [HttpPost("disconnect")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(WhatsAppStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disconnect(CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.DisconnectAsync(ct));

    [HttpPost("send")]
    [Authorize(Policy = PermissionPolicies.SettingsManage)]
    [ProducesResponseType(typeof(SendWhatsAppResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendText([FromBody] SendWhatsAppTextRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.SendTextAsync(request.Phone, request.Message, ct));

    [HttpPost("send-invoice/{sessionId:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(typeof(SendWhatsAppResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendInvoice(Guid sessionId, CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.SendInvoiceAsync(sessionId, ct));

    [HttpPost("send-offer")]
    [Authorize(Policy = PermissionPolicies.CustomersManage)]
    [ProducesResponseType(typeof(SendWhatsAppResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendOffer([FromBody] SendOfferRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _whatsApp.SendOfferAsync(request.CustomerId, request.OfferId, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
