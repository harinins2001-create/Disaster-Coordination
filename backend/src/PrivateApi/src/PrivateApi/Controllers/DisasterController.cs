using Core.Api.Response;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PrivateApi.Controllers;

/// <summary>
/// Admin CRUD endpoints for disasters.
/// </summary>
[Route("api/[controller]")]
public class DisasterController : ResponseController
{
    private const int MaxPhotos = 10;
    private const int MinPhotos = 1;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif", "image/heic"
    };

    private readonly IDisasterService _disasterService;
    private readonly IUserService _userService;
    private readonly IS3Service _s3Service;
    private readonly IEmailService _emailService;

    public DisasterController(IDisasterService disasterService, IUserService userService, IS3Service s3Service, IEmailService emailService)
    {
        _disasterService = disasterService;
        _userService = userService;
        _s3Service = s3Service;
        _emailService = emailService;
    }

    public class CreateDisasterRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int RequiredVolunteers { get; set; }
        public List<RequiredResourceDTO> RequiredResources { get; set; } = new();
        public List<IFormFile> Photos { get; set; } = new();
    }

    public class UpdateDisasterRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }
        public int? RequiredVolunteers { get; set; }
        public List<RequiredResourceDTO>? RequiredResources { get; set; }
    }

    public class RejectRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    private string? GetCallerSub() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst("cognito:username")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private async Task<(UserDTO? user, bool isAdminOrModerator, string? err)> GetAdminOrModeratorCaller(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return (null, false, "Unauthenticated");
        var me = await _userService.GetBySub(sub, ct);
        if (me is null || !me.Active) return (null, false, "Account disabled");
        var ok = me.Roles.Any(r =>
            string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase));
        return (me, ok, ok ? null : "Only admin or moderator");
    }

    /// <summary>List pending disaster submissions (admin/moderator only).</summary>
    [HttpGet("pending")]
    [Tags("Disaster")]
    public async Task<IActionResult> ListPending(CancellationToken ct)
    {
        var (_, isAdminOrMod, err) = await GetAdminOrModeratorCaller(ct);
        if (!isAdminOrMod) return ApiError(StatusCodes.Status403Forbidden, err ?? "Forbidden");

        var items = await _disasterService.GetDisastersByStatus("pending", ct);
        return ApiSuccess(items);
    }

    /// <summary>List all disasters regardless of status (admin/moderator only).</summary>
    [HttpGet("all")]
    [Tags("Disaster")]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        var (_, isAdminOrMod, err) = await GetAdminOrModeratorCaller(ct);
        if (!isAdminOrMod) return ApiError(StatusCodes.Status403Forbidden, err ?? "Forbidden");

        var items = await _disasterService.GetAllDisasters(ct);
        return ApiSuccess(items);
    }

    /// <summary>List disasters submitted by the current user (any status).</summary>
    [HttpGet("mine")]
    [Tags("Disaster")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        var items = await _disasterService.GetDisastersBySubmitter(sub, ct);
        return ApiSuccess(items);
    }

    /// <summary>Approve a pending disaster (admin/moderator only).</summary>
    [HttpPost("{slug}/approve")]
    [Tags("Disaster")]
    public async Task<IActionResult> Approve(string slug, CancellationToken ct)
    {
        var (_, isAdminOrMod, err) = await GetAdminOrModeratorCaller(ct);
        if (!isAdminOrMod) return ApiError(StatusCodes.Status403Forbidden, err ?? "Forbidden");

        var existing = await _disasterService.GetDisasterBySlug(slug, ct);
        if (existing is null) return ApiError(StatusCodes.Status404NotFound, "Disaster not found");
        if (!string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return ApiError(StatusCodes.Status400BadRequest, "Only pending submissions can be approved");
        }

        var updated = await _disasterService.SetStatus(slug, "active", string.Empty, ct);
        if (updated is null) return ApiError(StatusCodes.Status500InternalServerError, "Failed to approve");

        await _disasterService.RecomputeNeedsMet(slug, ct);

        if (!string.IsNullOrWhiteSpace(existing.ReportedBySub))
        {
            var submitter = await _userService.GetBySub(existing.ReportedBySub, ct);
            if (submitter is not null && !string.IsNullOrWhiteSpace(submitter.Email))
            {
                await _emailService.SendDisasterApprovedEmail(submitter.Email, submitter.Name ?? submitter.Email, existing.Title, slug, ct);
            }
        }

        return ApiSuccess(updated, "Disaster approved");
    }

    /// <summary>Reject a pending disaster with a reason (admin/moderator only).</summary>
    [HttpPost("{slug}/reject")]
    [Tags("Disaster")]
    public async Task<IActionResult> Reject(string slug, [FromBody] RejectRequest request, CancellationToken ct)
    {
        var (_, isAdminOrMod, err) = await GetAdminOrModeratorCaller(ct);
        if (!isAdminOrMod) return ApiError(StatusCodes.Status403Forbidden, err ?? "Forbidden");

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiError(StatusCodes.Status400BadRequest, "Reason is required",
                new Dictionary<string, string> { { "reason", "Reason is required" } });
        }

        var existing = await _disasterService.GetDisasterBySlug(slug, ct);
        if (existing is null) return ApiError(StatusCodes.Status404NotFound, "Disaster not found");
        if (!string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return ApiError(StatusCodes.Status400BadRequest, "Only pending submissions can be rejected");
        }

        var updated = await _disasterService.SetStatus(slug, "rejected", request.Reason, ct);
        if (updated is null) return ApiError(StatusCodes.Status500InternalServerError, "Failed to reject");

        if (!string.IsNullOrWhiteSpace(existing.ReportedBySub))
        {
            var submitter = await _userService.GetBySub(existing.ReportedBySub, ct);
            if (submitter is not null && !string.IsNullOrWhiteSpace(submitter.Email))
            {
                await _emailService.SendDisasterRejectedEmail(submitter.Email, submitter.Name ?? submitter.Email, existing.Title, request.Reason, ct);
            }
        }

        return ApiSuccess(updated, "Disaster rejected");
    }

    /// <summary>Get a single disaster by slug. Pending/rejected visible only to admin/moderator/submitter.</summary>
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
        if (disaster is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                new Dictionary<string, string> { { "slug", "Not found" } });
        }

        var isHidden = string.Equals(disaster.Status, "pending", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(disaster.Status, "rejected", StringComparison.OrdinalIgnoreCase);
        if (isHidden)
        {
            var sub = GetCallerSub();
            var me = string.IsNullOrWhiteSpace(sub) ? null : await _userService.GetBySub(sub, ct);
            var canView = me is not null && me.Active && (
                me.Roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase))
                || string.Equals(disaster.ReportedBySub, me.Sub, StringComparison.Ordinal));
            if (!canView)
            {
                return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                    new Dictionary<string, string> { { "slug", "Not found" } });
            }
        }

        return ApiSuccess(disaster);
    }

    /// <summary>Create a new disaster (multipart: title, description, location, severity, photos[]).</summary>
    [HttpPost]
    [Tags("Disaster")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateDisaster([FromForm] CreateDisasterRequest request, CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        var me = await _userService.GetBySub(sub, ct);
        if (me is null || !me.Active) return ApiError(StatusCodes.Status403Forbidden, "Account disabled");

        var fields = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Title)) fields["title"] = "Title is required";
        if (string.IsNullOrWhiteSpace(request.Description)) fields["description"] = "Description is required";
        if (string.IsNullOrWhiteSpace(request.Location)) fields["location"] = "Location is required";

        var photos = (request.Photos ?? new List<IFormFile>()).Where(p => p is not null && p.Length > 0).ToList();
        if (photos.Count < MinPhotos) fields["photos"] = $"At least {MinPhotos} photo required";
        else if (photos.Count > MaxPhotos) fields["photos"] = $"Maximum {MaxPhotos} photos allowed";
        else
        {
            foreach (var p in photos)
            {
                if (p.Length > MaxPhotoBytes) { fields["photos"] = "Each photo must be under 5MB"; break; }
                if (!AllowedPhotoTypes.Contains(p.ContentType ?? string.Empty)) { fields["photos"] = "Unsupported image type"; break; }
            }
        }

        if (fields.Count > 0) return ApiError(StatusCodes.Status400BadRequest, "Invalid submission", fields);

        var isAdminOrModerator = me.Roles.Any(r =>
            string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase));
        var status = isAdminOrModerator ? "active" : "pending";

        var keys = new Keys();
        var slug = keys.NameToSlug(request.Title);
        if (string.IsNullOrWhiteSpace(slug)) slug = Guid.NewGuid().ToString("N")[..12];

        var photoKeys = new List<string>();
        try
        {
            foreach (var photo in photos)
            {
                var key = await _s3Service.UploadDisasterPhoto(slug, photo, ct);
                photoKeys.Add(key);
            }
        }
        catch
        {
            foreach (var k in photoKeys)
            {
                try { await _s3Service.DeleteObject(k, ct); } catch { /* best-effort */ }
            }
            return ApiError(StatusCodes.Status500InternalServerError, "Failed to upload one or more photos");
        }

        var dto = new DisasterDTO
        {
            Slug = slug,
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity ?? string.Empty,
            Location = request.Location,
            Status = status,
            ReportedBy = me.Name,
            ReportedBySub = me.Sub,
            ReportedByName = me.Name,
            RequiredVolunteers = Math.Max(0, request.RequiredVolunteers),
            RequiredResources = request.RequiredResources ?? new List<RequiredResourceDTO>(),
            PhotoKeys = photoKeys
        };

        var created = await _disasterService.CreateDisaster(dto, ct);
        if (created is null)
        {
            foreach (var k in photoKeys)
            {
                try { await _s3Service.DeleteObject(k, ct); } catch { /* best-effort */ }
            }
            return ApiError(StatusCodes.Status500InternalServerError, "Failed to create disaster");
        }

        return ApiSuccess(created, status == "pending" ? "Submitted for review" : "Disaster created");
    }

    /// <summary>Update an existing disaster by slug.</summary>
    [HttpPut("{slug}")]
    [Tags("Disaster")]
    public async Task<IActionResult> UpdateDisaster(string slug, [FromBody] UpdateDisasterRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ApiValidationError(ModelState);

        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");
        var me = await _userService.GetBySub(sub, ct);
        if (me is null || !me.Active) return ApiError(StatusCodes.Status403Forbidden, "Account disabled");

        var existing = await _disasterService.GetDisasterBySlug(slug, ct);
        if (existing is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                new Dictionary<string, string> { { "slug", "Not found" } });
        }

        var canModify = me.Roles.Any(r =>
            string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase));
        if (!canModify)
        {
            return ApiError(StatusCodes.Status403Forbidden, "Only admin or moderator can update this disaster");
        }

        var patch = new DisasterDTO
        {
            Title = request.Title ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Severity = request.Severity ?? string.Empty,
            Location = request.Location ?? string.Empty,
            Status = request.Status ?? string.Empty,
            RequiredVolunteers = request.RequiredVolunteers ?? 0,
            RequiredResources = request.RequiredResources
        };

        var updated = await _disasterService.UpdateDisaster(slug, patch, ct);
        if (updated is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                new Dictionary<string, string> { { "slug", "Not found" } });
        }

        await _disasterService.RecomputeNeedsMet(slug, ct);
        return ApiSuccess(updated, "Disaster updated");
    }

    /// <summary>Delete a disaster by slug. Submitter can delete own pending submission; admin/moderator can delete anytime.</summary>
    [HttpDelete("{slug}")]
    [Tags("Disaster")]
    public async Task<IActionResult> DeleteDisaster(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ApiError(StatusCodes.Status400BadRequest, "Slug is required",
                new Dictionary<string, string> { { "slug", "Slug is required" } });
        }

        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");
        var me = await _userService.GetBySub(sub, ct);
        if (me is null || !me.Active) return ApiError(StatusCodes.Status403Forbidden, "Account disabled");

        var existing = await _disasterService.GetDisasterBySlug(slug, ct);
        if (existing is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "Disaster not found",
                new Dictionary<string, string> { { "slug", "Not found" } });
        }

        var isAdminOrModerator = me.Roles.Any(r =>
            string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "moderator", StringComparison.OrdinalIgnoreCase));
        var isOwningPendingSubmitter =
            string.Equals(existing.ReportedBySub, me.Sub, StringComparison.Ordinal) &&
            string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase);

        if (!isAdminOrModerator && !isOwningPendingSubmitter)
        {
            return ApiError(StatusCodes.Status403Forbidden,
                "Only admin/moderator can delete, or submitter can delete their own pending submission");
        }

        foreach (var key in existing.PhotoKeys ?? new List<string>())
        {
            try { await _s3Service.DeleteObject(key, ct); } catch { /* best-effort */ }
        }

        await _disasterService.DeleteDisaster(slug, ct);
        return ApiSuccess(new { slug }, "Disaster deleted");
    }
}
