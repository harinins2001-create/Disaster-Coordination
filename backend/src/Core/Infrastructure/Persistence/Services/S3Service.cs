using Amazon.S3;
using Amazon.S3.Model;
using Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Core.Infrastructure.Persistence.Services;

public class S3Service : IS3Service
{
    private static readonly Dictionary<string, string> ExtByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["image/heic"] = ".heic",
    };

    private static readonly TimeSpan DefaultPresignedExpiry = TimeSpan.FromHours(6);

    private readonly string _bucketName;

    public S3Service()
    {
        _bucketName = Environment.GetEnvironmentVariable("ASSETS_BUCKET")
                      ?? throw new Exception("ASSETS_BUCKET env var not set");
    }

    public async Task<string> UploadUserPhoto(string sub, IFormFile file, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sub)) throw new ArgumentException("sub required", nameof(sub));
        return await UploadImage($"users/{sub}", file, ct);
    }

    public async Task<string> UploadDisasterPhoto(string slug, IFormFile file, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("slug required", nameof(slug));
        return await UploadImage($"disasters/{slug}", file, ct);
    }

    private async Task<string> UploadImage(string prefix, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("file required", nameof(file));

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var ext = ResolveExtension(contentType, file.FileName);
        var key = $"{prefix}/{Guid.NewGuid():N}{ext}";

        using var client = new AmazonS3Client(Amazon.RegionEndpoint.APSouth1);
        await using var stream = file.OpenReadStream();

        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);

        return key;
    }

    public string? GetPresignedUrl(string? key, TimeSpan? expiresIn = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        using var client = new AmazonS3Client(Amazon.RegionEndpoint.APSouth1);
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn ?? DefaultPresignedExpiry),
            Protocol = Protocol.HTTPS
        };
        return client.GetPreSignedURL(req);
    }

    public async Task DeleteObject(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        using var client = new AmazonS3Client(Amazon.RegionEndpoint.APSouth1);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        }, ct);
    }

    private static string ResolveExtension(string contentType, string? fileName)
    {
        if (ExtByContentType.TryGetValue(contentType, out var mapped)) return mapped;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(ext)) return ext.ToLowerInvariant();
        }

        return string.Empty;
    }
}
