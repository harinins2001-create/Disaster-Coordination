using Core.DTOs;

namespace Core.Interfaces;

public interface IResourceService
{
    Task<List<ResourceDTO>> GetResourcesByDisaster(string disasterSlug, CancellationToken ct = default);
    Task<ResourceDTO?> UpsertResource(string disasterSlug, string itemType, int quantity, CancellationToken ct = default);
    Task<bool> DeleteResource(string disasterSlug, string itemType, CancellationToken ct = default);
}
