using Core.Api.Response;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PrivateApi.Controllers;

[Route("api/[controller]")]
public class UserController : ResponseController
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/heic"
    };

    private readonly IUserService _userService;
    private readonly IS3Service _s3Service;

    public UserController(IUserService userService, IS3Service s3Service)
    {
        _userService = userService;
        _s3Service = s3Service;
    }

    public class UpdateProfileRequest
    {
        public string? PhotoKey { get; set; }
        public string? Area { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? TravelMethods { get; set; }
    }

    public class SetRolesRequest
    {
        public List<string> Roles { get; set; } = new();
    }

    public class SetActiveRequest
    {
        public bool Active { get; set; }
    }

    public class AdminCreateRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nic { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Dob { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new();
        public List<string> TravelMethods { get; set; } = new();
        public List<string> Roles { get; set; } = new();
    }

    private string? GetCallerSub()
    {
        var sub = User.FindFirst("sub")?.Value
                  ?? User.FindFirst("cognito:username")?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return sub;
    }

    private async Task<UserDTO?> GetCaller(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return null;
        return await _userService.GetBySub(sub, ct);
    }

    private static bool IsAdmin(UserDTO? user) =>
        user is { Active: true } && user.Roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase));

    [HttpGet("me")]
    [Tags("User")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub))
        {
            return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");
        }

        var me = await _userService.GetBySub(sub, ct);
        if (me is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "User profile not found",
                new Dictionary<string, string> { { "sub", "Not found" } });
        }

        return ApiSuccess(me);
    }

    [HttpPut("me")]
    [Tags("User")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        var patch = new UserDTO
        {
            PhotoKey = request.PhotoKey ?? string.Empty,
            Area = request.Area ?? string.Empty,
            Skills = request.Skills ?? new List<string>(),
            TravelMethods = request.TravelMethods ?? new List<string>()
        };

        var updated = await _userService.UpdateProfile(sub, patch, ct);
        if (updated is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "User profile not found");
        }

        return ApiSuccess(updated, "Profile updated");
    }

    [HttpPost("me/photo")]
    [Tags("User")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyPhoto([FromForm] IFormFile? photo, CancellationToken ct)
    {
        var sub = GetCallerSub();
        if (string.IsNullOrWhiteSpace(sub)) return ApiError(StatusCodes.Status401Unauthorized, "Unauthenticated");

        if (photo is null || photo.Length == 0)
        {
            return ApiError(StatusCodes.Status400BadRequest, "photo file is required",
                new Dictionary<string, string> { { "photo", "A photo file is required" } });
        }
        if (photo.Length > MaxPhotoBytes)
        {
            return ApiError(StatusCodes.Status400BadRequest, "photo exceeds 5MB limit",
                new Dictionary<string, string> { { "photo", "File must be 5MB or smaller" } });
        }
        if (!AllowedPhotoTypes.Contains(photo.ContentType ?? string.Empty))
        {
            return ApiError(StatusCodes.Status400BadRequest, "unsupported image type",
                new Dictionary<string, string> { { "photo", "Allowed types: JPEG, PNG, WebP, GIF, HEIC" } });
        }

        var existing = await _userService.GetBySub(sub, ct);
        if (existing is null) return ApiError(StatusCodes.Status404NotFound, "User profile not found");
        var previousKey = existing.PhotoKey;

        var newKey = await _s3Service.UploadUserPhoto(sub, photo, ct);

        var updated = await _userService.UpdateProfile(sub, new UserDTO { PhotoKey = newKey }, ct);
        if (updated is null) return ApiError(StatusCodes.Status500InternalServerError, "Failed to update profile");

        if (!string.IsNullOrWhiteSpace(previousKey) && !string.Equals(previousKey, newKey, StringComparison.Ordinal))
        {
            try { await _s3Service.DeleteObject(previousKey, ct); }
            catch { /* best-effort: orphan is cheaper than failing the request */ }
        }

        return ApiSuccess(updated, "Photo updated");
    }

    [HttpGet]
    [Tags("User")]
    public async Task<IActionResult> List(
        [FromQuery] string? role,
        [FromQuery] string? district,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var caller = await GetCaller(ct);
        if (!IsAdmin(caller)) return ApiError(StatusCodes.Status403Forbidden, "Admin only");

        var users = await _userService.ListUsers(role, district, search, ct);
        return ApiSuccess(users);
    }

    [HttpPost]
    [Tags("User")]
    public async Task<IActionResult> AdminCreate([FromBody] AdminCreateRequest request, CancellationToken ct)
    {
        var caller = await GetCaller(ct);
        if (!IsAdmin(caller)) return ApiError(StatusCodes.Status403Forbidden, "Admin only");

        var dto = new SignupRequestDTO
        {
            Email = request.Email,
            Password = request.Password,
            Name = request.Name,
            Nic = request.Nic,
            Phone = request.Phone,
            Dob = request.Dob,
            Gender = request.Gender,
            Area = request.Area,
            Skills = request.Skills,
            TravelMethods = request.TravelMethods,
            Roles = request.Roles
        };

        var created = await _userService.AdminCreate(dto, ct);
        if (created is null)
        {
            return ApiError(StatusCodes.Status409Conflict, "Email or NIC already registered");
        }

        return ApiSuccess(created, "User created");
    }

    [HttpPut("{sub}/roles")]
    [Tags("User")]
    public async Task<IActionResult> SetRoles(string sub, [FromBody] SetRolesRequest request, CancellationToken ct)
    {
        var caller = await GetCaller(ct);
        if (!IsAdmin(caller)) return ApiError(StatusCodes.Status403Forbidden, "Admin only");

        var updated = await _userService.SetRoles(sub, request.Roles ?? new List<string>(), ct);
        if (updated is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "User not found");
        }

        return ApiSuccess(updated, "Roles updated");
    }

    [HttpPut("{sub}/active")]
    [Tags("User")]
    public async Task<IActionResult> SetActive(string sub, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        var caller = await GetCaller(ct);
        if (!IsAdmin(caller)) return ApiError(StatusCodes.Status403Forbidden, "Admin only");

        var updated = await _userService.SetActive(sub, request.Active, ct);
        if (updated is null)
        {
            return ApiError(StatusCodes.Status404NotFound, "User not found");
        }

        return ApiSuccess(updated, request.Active ? "User activated" : "User deactivated");
    }
}
