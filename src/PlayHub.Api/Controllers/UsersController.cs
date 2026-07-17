using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Users;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        await ExecuteAsync(() => _userService.GetUsersAsync(page, pageSize, ct));

    /// <summary>Permission catalog for staff assignment UI.</summary>
    [HttpGet("permissions")]
    [HttpGet("~/api/permissions")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct) =>
        await ExecuteAsync(() => _userService.GetPermissionsAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _userService.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _userService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _userService.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _userService.ResetPasswordAsync(id, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
