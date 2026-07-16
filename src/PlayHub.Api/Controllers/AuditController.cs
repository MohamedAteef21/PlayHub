using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Audit;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = PermissionPolicies.MasterOnly)]
public class AuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] AuditLogQuery query, CancellationToken ct)
    {
        try
        {
            return Ok(await _auditLogService.GetLogsAsync(query, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
