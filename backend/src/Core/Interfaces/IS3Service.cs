using Microsoft.AspNetCore.Http;

namespace Core.Interfaces;

public interface IS3Service
{
    Task<string> UploadUserPhoto(string sub, IFormFile file, CancellationToken ct = default);
    Task<string> UploadDisasterPhoto(string slug, IFormFile file, CancellationToken ct = default);
    string? GetPresignedUrl(string? key, TimeSpan? expiresIn = null);
    Task DeleteObject(string key, CancellationToken ct = default);
}
