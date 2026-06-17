using Core.Api.Response;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PrivateApi.Controllers;

[Route("api/[controller]")]
public class AssignmentController : ResponseController
{
    private readonly IAssignmentService _assignmentService;
    private readonly IUserService _userService;
    private readonly IDisasterService _disasterService;

    public AssignmentController(
        IAssignmentService assignmentService,
        IUserService userService,
        IDisasterService disasterService)
    {
        _assignmentService = assignmentService;
        _userService = userService;
        _disasterService = disasterService;
    }

    public class PledgeRequest
    {
        public string DisasterSlug { get; set; } = string.Empty;
    }

    public class StatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    private string? GetCallerSub() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst("cognito:username")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private async Task<(UserDTO? user, string? error)> GetCaller(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return (null, "Unauthenticated");
        var me = await _userService.GetBySub(sub, ct);
        if (me is null) return (null, "User profile not found");
        if (!me.Active) return (null, "Account is disabled");
        return (me, null);
    }

    private static bool CanVolunteer(UserDTO u) =>
        u.Roles.Any(r => string.Equals(r, "helper", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(r, "medic", StringComparison.OrdinalIgnoreCase));

    private static bool IsAdminOrModerator(UserDTO u) =>
        u.Roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase));

    [HttpPost("pledge")]
    [Tags("Assignment")]
    public async Task<IActionResult> Pledge([FromBody] PledgeRequest request, CancellationToken ct)
    {
        var (me, err) = await GetCaller(ct);
        if (me is null) return ApiError(StatusCodes.Status401Unauthorized, err ?? "Unauthenticated");
        if (!CanVolunteer(me))
        {
            return ApiError(StatusCodes.Status403Forbidden, "Only helpers or medics can volunteer");
        }

        if (string.IsNullOrWhiteSpace(request.DisasterSlug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug is required");
        }

        var disaster = await _disasterService.GetDisasterBySlug(request.DisasterSlug, ct);
        if (disaster is null) return ApiError(StatusCodes.Status404NotFound, "Disaster not found");

        var saved = await _assignmentService.Pledge(request.DisasterSlug, me.Sub, me.Name, me.Email, me.PhotoKey ?? string.Empty, ct);
        if (saved is null) return ApiError(StatusCodes.Status500InternalServerError, "Failed to pledge");

        await _disasterService.RecomputeNeedsMet(request.DisasterSlug, ct);
        return ApiSuccess(saved, "Volunteer pledge recorded");
    }

    [HttpDelete("{disasterSlug}")]
    [Tags("Assignment")]
    public async Task<IActionResult> Cancel(string disasterSlug, CancellationToken ct)
    {
        var (me, err) = await GetCaller(ct);
        if (me is null) return ApiError(StatusCodes.Status401Unauthorized, err ?? "Unauthenticated");

        var ok = await _assignmentService.Cancel(disasterSlug, me.Sub, ct);
        if (!ok) return ApiError(StatusCodes.Status404NotFound, "Assignment not found");

        await _disasterService.RecomputeNeedsMet(disasterSlug, ct);
        return ApiSuccess(new { disasterSlug }, "Volunteer pledge cancelled");
    }

    [HttpPut("{disasterSlug}/{userSub}/status")]
    [Tags("Assignment")]
    public async Task<IActionResult> SetStatus(string disasterSlug, string userSub, [FromBody] StatusRequest request, CancellationToken ct)
    {
        var (me, err) = await GetCaller(ct);
        if (me is null) return ApiError(StatusCodes.Status401Unauthorized, err ?? "Unauthenticated");
        if (!IsAdminOrModerator(me))
        {
            return ApiError(StatusCodes.Status403Forbidden, "Only admin or moderator can change volunteer status");
        }

        var updated = await _assignmentService.SetStatus(disasterSlug, userSub, request.Status, ct);
        if (updated is null)
        {
            return ApiError(StatusCodes.Status400BadRequest, "Invalid status or assignment not found");
        }

        await _disasterService.RecomputeNeedsMet(disasterSlug, ct);
        return ApiSuccess(updated, "Status updated");
    }

    [HttpGet("me")]
    [Tags("Assignment")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var (me, err) = await GetCaller(ct);
        if (me is null) return ApiError(StatusCodes.Status401Unauthorized, err ?? "Unauthenticated");

        var items = await _assignmentService.GetByUser(me.Sub, ct);
        return ApiSuccess(items);
    }

    [HttpGet]
    [Tags("Assignment")]
    public async Task<IActionResult> ListByDisaster([FromQuery] string disasterSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disasterSlug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug is required");
        }

        var items = await _assignmentService.GetByDisaster(disasterSlug, ct);
        return ApiSuccess(items);
    }
}
