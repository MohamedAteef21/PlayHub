using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Infrastructure.Data;
using PlayHub.Infrastructure.Services;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly DataWipeService _wipe;
    private readonly TenantContext _tenant;

    public AdminController(DataWipeService wipe, TenantContext tenant)
    {
        _wipe = wipe;
        _tenant = tenant;
    }

    /// <summary>
    /// Hard-delete all operational data. Keeps tenants, users, branches, user-branches, permissions.
    /// SuperAdmin only. Irreversible.
    /// </summary>
    [HttpPost("wipe-operational-data")]
    public async Task<IActionResult> WipeOperationalData(
        [FromQuery] string confirm,
        CancellationToken ct)
    {
        if (!_tenant.IsSuperAdmin)
            return Forbid();

        if (!string.Equals(confirm, "WIPE_ALL_EXCEPT_USERS", StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Pass ?confirm=WIPE_ALL_EXCEPT_USERS to proceed. This permanently deletes sessions, inventory, assets, invoices, cash, customers, etc. Users/branches are kept."
            });
        }

        try
        {
            var result = await _wipe.WipeOperationalDataAsync(ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
