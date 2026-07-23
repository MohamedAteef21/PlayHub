using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayHub.Api.Authorization;
using PlayHub.Application.Cafeteria;
using PlayHub.Application.Sessions;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService) => _sessionService = sessionService;

    /// <summary>All open/paused sessions for the active branch — live timer data included.</summary>
    [HttpGet("active")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(IReadOnlyList<SessionLiveDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.GetActiveSessionsAsync(ct));

    /// <summary>All sessions for the branch (newest first), optional date filter on StartedAt.</summary>
    [HttpGet("history")]
    [Authorize(Policy = PermissionPolicies.SessionsHistory)]
    [ProducesResponseType(typeof(IReadOnlyList<SessionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null,
        CancellationToken ct = default) =>
        await ExecuteAsync(() => _sessionService.GetSessionHistoryAsync(from, to, page, pageSize, customerId, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SessionsView)]
    [ProducesResponseType(typeof(SessionDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var session = await _sessionService.GetSessionByIdAsync(id, ct);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("open")]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Open([FromBody] OpenSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.OpenSessionAsync(request, ct), StatusCodes.Status201Created);

    [HttpPost("{id:guid}/pause")]
    [Authorize(Policy = PermissionPolicies.SessionsPause)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.PauseSessionAsync(id, ct));

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = PermissionPolicies.SessionsPause)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.ResumeSessionAsync(id, ct));

    /// <summary>Add time to a booked session, or switch it to an open timer (additionalMinutes = null).</summary>
    [HttpPost("{id:guid}/extend")]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.ExtendSessionAsync(id, request, ct));

    /// <summary>Change the watcher headcount on an active watching session.</summary>
    [HttpPost("{id:guid}/watchers")]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWatchers(Guid id, [FromBody] UpdateWatchersRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.UpdateWatcherCountAsync(id, request, ct));

    /// <summary>Convert watching → gaming (individual/couple) and start hourly timer.</summary>
    [HttpPost("{id:guid}/convert")]
    [Authorize(Policy = PermissionPolicies.SessionsCreate)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.ConvertSessionAsync(id, request, ct));

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = PermissionPolicies.SessionsClose)]
    [ProducesResponseType(typeof(SessionDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSessionRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.CloseSessionAsync(id, request, ct));

    [HttpPost("{id:guid}/cafeteria")]
    [Authorize(Policy = PermissionPolicies.CafeteriaSell)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCafeteria(Guid id, [FromBody] AddSessionCafeteriaRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.AddCafeteriaItemAsync(id, request, ct));

    [HttpPost("{id:guid}/cafeteria/returns")]
    [Authorize(Policy = PermissionPolicies.CafeteriaReturn)]
    [ProducesResponseType(typeof(SessionLiveDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReturnCafeteria(Guid id, [FromBody] ReturnSessionCafeteriaRequest request, CancellationToken ct) =>
        await ExecuteAsync(() => _sessionService.ReturnCafeteriaItemAsync(id, request, ct));

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
        catch (MissingIngredientsException ex)
        {
            return Conflict(new
            {
                code = MissingIngredientsException.ErrorCode,
                message = ex.Message,
                missing = ex.Missing
            });
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
