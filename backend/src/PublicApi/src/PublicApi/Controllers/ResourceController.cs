using Core.Api.Response;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PublicApi.Controllers;

/// <summary>
/// Read-only public endpoints for disaster resources.
/// </summary>
[Route("api/[controller]")]
public class ResourceController : ResponseController
{
    private readonly IResourceService _resourceService;

    public ResourceController(IResourceService resourceService)
    {
        _resourceService = resourceService;
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
}
