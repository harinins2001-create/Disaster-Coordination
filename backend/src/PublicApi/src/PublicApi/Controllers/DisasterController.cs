using Core.Api.Response;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PublicApi.Controllers;

/// <summary>
/// Read-only public endpoints for disasters.
/// </summary>
[Route("api/[controller]")]
public class DisasterController : ResponseController
{
    private readonly IDisasterService _disasterService;

    public DisasterController(IDisasterService disasterService)
    {
        _disasterService = disasterService;
    }

    private static readonly HashSet<string> HiddenStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "pending", "rejected" };

    /// <summary>List all publicly visible disasters (excludes pending and rejected).</summary>
    [HttpGet]
    [Tags("Disaster")]
    public async Task<IActionResult> GetAllDisasters(CancellationToken ct)
    {
        var items = await _disasterService.GetAllDisasters(ct);
        var visible = items.Where(d => !HiddenStatuses.Contains(d.Status ?? string.Empty)).ToList();
        return ApiSuccess(visible);
    }

    /// <summary>Get a single publicly visible disaster by slug (404s pending and rejected).</summary>
    [HttpGet("{slug}")]
    [Tags("Disaster")]
    public async Task<IActionResult> GetDisaster(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "Slug is required",
                new Dictionary<string, string> { { "slug", "Slug is required" } });
        }

        var disaster = await _disasterService.GetDisasterBySlug(slug, ct);
        if (disaster is null || HiddenStatuses.Contains(disaster.Status ?? string.Empty))
        {
            return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                new Dictionary<string, string> { { "slug", "Not found" } });
        }

        return ApiSuccess(disaster);
    }
}
