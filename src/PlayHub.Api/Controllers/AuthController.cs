using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Application.Auth;
using PlayHub.Domain.Enums;
using System.Security.Claims;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Register a new tenant with master user and first branch.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterTenantRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.RegisterTenantAsync(request, ct);
            return Created(string.Empty, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Login with username and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Refresh access token using a valid refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.RefreshTokenAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            var expired = ex.Message.Contains("SUBSCRIPTION_EXPIRED", StringComparison.OrdinalIgnoreCase);
            return Unauthorized(new
            {
                message = ex.Message,
                code = expired ? "SUBSCRIPTION_EXPIRED" : "UNAUTHORIZED"
            });
        }
    }

    /// <summary>Revoke a refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Select active branch and get a new token with branch context.</summary>
    [HttpPost("select-branch")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectBranch([FromBody] SelectBranchRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            return Ok(await _authService.SelectBranchAsync(userId, request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get current authenticated user profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var isMaster = bool.Parse(User.FindFirst("is_master")?.Value ?? "false");
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var role = int.TryParse(roleClaim, out var roleInt)
            ? (UserRole)roleInt
            : isMaster ? UserRole.SuperAdmin : UserRole.Staff;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = new AuthUserDto(
            userId,
            User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            User.FindFirst("given_name")?.Value ?? string.Empty,
            User.FindFirst("family_name")?.Value ?? string.Empty,
            isMaster,
            role,
            null,
            null,
            null,
            User.FindAll("permission").Select(c => c.Value).ToList(),
            User.FindAll("branch").Select(c => new BranchDto(Guid.Parse(c.Value), string.Empty, false)).ToList());

        return Ok(user);
    }

    /// <summary>Save language and theme preferences for the current user.</summary>
    [HttpPut("preferences")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUiPreferencesRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            return Ok(await _authService.UpdateUiPreferencesAsync(userId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
