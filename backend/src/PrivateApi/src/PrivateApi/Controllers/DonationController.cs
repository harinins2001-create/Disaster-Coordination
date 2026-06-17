using Core.Api.Response;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PrivateApi.Controllers;

[Route("api/[controller]")]
public class DonationController : ResponseController
{
    private readonly IDonationService _donationService;
    private readonly IResourceService _resourceService;
    private readonly IUserService _userService;
    private readonly IDisasterService _disasterService;

    public DonationController(
        IDonationService donationService,
        IResourceService resourceService,
        IUserService userService,
        IDisasterService disasterService)
    {
        _donationService = donationService;
        _resourceService = resourceService;
        _userService = userService;
        _disasterService = disasterService;
    }

    public class CreateDonationRequest
    {
        public string DisasterSlug { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    private string? GetCallerSub() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst("cognito:username")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [HttpPost]
    [Tags("Donation")]
    public async Task<IActionResult> Create([FromBody] CreateDonationRequest request, CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        var me = await _userService.GetBySub(sub, ct);
        if (me is null) return ApiError(StatusCodes.Status404NotFound, "User profile not found");
        if (!me.Active) return ApiError(StatusCodes.Status403Forbidden, "Account disabled");

        if (string.IsNullOrWhiteSpace(request.DisasterSlug) ||
            string.IsNullOrWhiteSpace(request.ItemType) ||
            request.Quantity <= 0)
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug, itemType, and positive quantity are required");
        }

        var disaster = await _disasterService.GetDisasterBySlug(request.DisasterSlug, ct);
        if (disaster is null) return ApiError(StatusCodes.Status404NotFound, "Disaster not found");

        var dto = new DonationDTO
        {
            DisasterSlug = request.DisasterSlug,
            UserSub = me.Sub,
            UserName = me.Name,
            ItemType = request.ItemType,
            Quantity = request.Quantity,
            Note = request.Note
        };

        var created = await _donationService.Create(dto, ct);
        if (created is null) return ApiError(StatusCodes.Status500InternalServerError, "Failed to record donation");

        var existingResources = await _resourceService.GetResourcesByDisaster(request.DisasterSlug, ct);
        var existing = existingResources.FirstOrDefault(r => string.Equals(r.ItemType, request.ItemType, StringComparison.OrdinalIgnoreCase));
        var newQty = (existing?.Quantity ?? 0) + request.Quantity;
        await _resourceService.UpsertResource(request.DisasterSlug, request.ItemType, newQty, ct);

        await _disasterService.RecomputeNeedsMet(request.DisasterSlug, ct);

        return ApiSuccess(created, "Donation recorded");
    }

    [HttpGet("me")]
    [Tags("Donation")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        var items = await _donationService.GetByUser(sub, ct);
        return ApiSuccess(items);
    }

    [HttpGet]
    [Tags("Donation")]
    public async Task<IActionResult> ListByDisaster([FromQuery] string disasterSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disasterSlug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "disasterSlug is required");
        }

        var items = await _donationService.GetByDisaster(disasterSlug, ct);
        return ApiSuccess(items);
    }
}
