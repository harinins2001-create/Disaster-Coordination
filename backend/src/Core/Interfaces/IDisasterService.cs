using Core.DTOs;

namespace Core.Interfaces;

public interface IDisasterService
{
    Task<List<DisasterDTO>> GetAllDisasters(CancellationToken ct = default);
    Task<List<DisasterDTO>> GetDisastersByStatus(string status, CancellationToken ct = default);
    Task<List<DisasterDTO>> GetDisastersBySubmitter(string sub, CancellationToken ct = default);
    Task<DisasterDTO?> GetDisasterBySlug(string slug, CancellationToken ct = default);
    Task<DisasterDTO?> CreateDisaster(DisasterDTO dto, CancellationToken ct = default);
    Task<DisasterDTO?> UpdateDisaster(string slug, DisasterDTO dto, CancellationToken ct = default);
    Task<DisasterDTO?> SetStatus(string slug, string status, string? rejectionReason, CancellationToken ct = default);
    Task<bool> DeleteDisaster(string slug, CancellationToken ct = default);
    Task<DisasterDTO?> RecomputeNeedsMet(string slug, CancellationToken ct = default);
}
