using System.Text.RegularExpressions;
using Core.Api.Response;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PublicApi.Controllers;

[Route("api/[controller]")]
public class AuthController : ResponseController
{
    private static readonly Regex NicRegex = new(@"^(\d{9}[VvXx]|\d{12})$");
    private static readonly HashSet<string> Districts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Colombo","Gampaha","Kalutara","Kandy","Matale","Nuwara Eliya","Galle","Matara","Hambantota",
        "Jaffna","Kilinochchi","Mannar","Vavuniya","Mullaitivu","Batticaloa","Ampara","Trincomalee",
        "Kurunegala","Puttalam","Anuradhapura","Polonnaruwa","Badulla","Monaragala","Ratnapura","Kegalle"
    };

    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPhotoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif", "image/heic"
    };

    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("signup")]
    [Tags("Auth")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Signup([FromForm] SignupRequestDTO request, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.Email)) fields["email"] = "Email is required";
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8) fields["password"] = "Password must be at least 8 characters";
        if (string.IsNullOrWhiteSpace(request.Name)) fields["name"] = "Name is required";
        if (string.IsNullOrWhiteSpace(request.Nic) || !NicRegex.IsMatch(request.Nic)) fields["nic"] = "NIC must match Sri Lankan format";
        if (string.IsNullOrWhiteSpace(request.Dob)) fields["dob"] = "DOB is required";
        if (request.Gender != "M" && request.Gender != "F") fields["gender"] = "Gender must be M or F";
        if (string.IsNullOrWhiteSpace(request.Area) || !Districts.Contains(request.Area)) fields["area"] = "Area must be a Sri Lankan district";

        if (request.Photo is not null)
        {
            if (request.Photo.Length > MaxPhotoBytes) fields["photo"] = "Photo must be under 5MB";
            else if (!AllowedPhotoContentTypes.Contains(request.Photo.ContentType ?? "")) fields["photo"] = "Photo must be JPEG, PNG, WebP, GIF or HEIC";
        }

        if (fields.Count > 0) return ApiError(StatusCodes.Status400BadRequest, "Validation error", fields);

        var existingEmail = await _userService.GetByEmail(request.Email, ct);
        if (existingEmail is not null)
        {
            return ApiError(StatusCodes.Status409Conflict, "Email already registered",
                new Dictionary<string, string> { { "email", "Already registered" } });
        }

        var existingNic = await _userService.GetByNic(request.Nic, ct);
        if (existingNic is not null)
        {
            return ApiError(StatusCodes.Status409Conflict, "NIC already registered",
                new Dictionary<string, string> { { "nic", "Already registered" } });
        }

        var created = await _userService.SignupPublic(request, ct);
        if (created is null)
        {
            return ApiError(StatusCodes.Status500InternalServerError, "Failed to create user");
        }

        return ApiSuccess(created, "Signup successful");
    }
}
