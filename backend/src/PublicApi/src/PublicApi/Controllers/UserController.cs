using Core.Api.Response;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PublicApi.Controllers;

[Route("api/[controller]")]
public class UserController : ResponseController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("exists")]
    [Tags("User")]
    public async Task<IActionResult> CheckExists(
        [FromQuery] string? email,
        [FromQuery] string? nic,
        CancellationToken ct)
    {
        var result = new { emailExists = false, nicExists = false };

        var emailExists = false;
        var nicExists = false;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _userService.GetByEmail(email, ct);
            emailExists = byEmail is not null;
        }

        if (!string.IsNullOrWhiteSpace(nic))
        {
            var byNic = await _userService.GetByNic(nic, ct);
            nicExists = byNic is not null;
        }

        return ApiSuccess(new { emailExists, nicExists });
    }
}
