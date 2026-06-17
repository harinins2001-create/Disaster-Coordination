using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Core.Api.Response;

public abstract class ResponseController : ControllerBase
{
    protected IActionResult ApiSuccess(object? data = null, string? message = null)
        => Ok(new { success = true, message, data });

    protected IActionResult ApiError(int statusCode, string message, IDictionary<string, string>? fields = null)
        => StatusCode(statusCode, new { success = false, message, fields });

    protected IActionResult ApiValidationError(ModelStateDictionary modelState)
    {
        var fields = modelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => string.Join("; ", kv.Value!.Errors.Select(e => e.ErrorMessage)));

        return StatusCode(StatusCodes.Status400BadRequest,
            new { success = false, message = "Validation error", fields });
    }
}
