using Core.Api.Response;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PrivateApi.Controllers;

/// <summary>
/// Admin endpoints for disaster resources (by item type).
/// </summary>
[Route("api/[controller]")]
public class ResourceController : ResponseController
{
    private readonly IResourceService _resourceService;
    private readonly IDisasterService _disasterService;

    public ResourceController(IResourceService resourceService, IDisasterService disasterService)
    {
        _resourceService = resourceService;
        _disasterService = disasterService;
    }

    public class UpsertResourceRequest
    {
        public int Quantity { get; set; }
    }

    /// <summary>List all resources for a disaster.</summary>
    [HttpGet]
    [Tags("Resource")]
    public async Task<IActionResult> GetResources([FromQuery] string disasterSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disasterSlug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug is required",
                new Dictionary<string, string> { { "disasterSlug", "Required" } });
        }

        var items = await _resourceService.GetResourcesByDisaster(disasterSlug, ct);
        return ApiSuccess(items);
    }

    /// <summary>Set the quantity for an item type on a disaster (upsert).</summary>
    [HttpPut("{disasterSlug}/{itemType}")]
    [Tags("Resource")]
    public async Task<IActionResult> Upsert(string disasterSlug, string itemType, [FromBody] UpsertResourceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disasterSlug) || string.IsNullOrWhiteSpace(itemType))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug and itemType required");
        }

        var saved = await _resourceService.UpsertResource(disasterSlug, itemType, request.Quantity, ct);
        await _disasterService.RecomputeNeedsMet(disasterSlug, ct);
        return ApiSuccess(saved, "Resource saved");
    }

    /// <summary>Delete an item-type entry from a disaster.</summary>
    [HttpDelete("{disasterSlug}/{itemType}")]
    [Tags("Resource")]
    public async Task<IActionResult> Delete(string disasterSlug, string itemType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disasterSlug) || string.IsNullOrWhiteSpace(itemType))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug and itemType required");
        }

        await _resourceService.DeleteResource(disasterSlug, itemType, ct);
        await _disasterService.RecomputeNeedsMet(disasterSlug, ct);
        return ApiSuccess(new { disasterSlug, itemType }, "Resource deleted");
    }
}
