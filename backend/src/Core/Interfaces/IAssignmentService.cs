using Core.DTOs;

namespace Core.Interfaces;

public interface IAssignmentService
{
    Task<AssignmentDTO?> Pledge(string disasterSlug, string userSub, string userName, string userEmail, string userPhotoKey, CancellationToken ct = default);
    Task<AssignmentDTO?> SetStatus(string disasterSlug, string userSub, string status, CancellationToken ct = default);
    Task<AssignmentDTO?> GetOne(string disasterSlug, string userSub, CancellationToken ct = default);
    Task<List<AssignmentDTO>> GetByDisaster(string disasterSlug, CancellationToken ct = default);
    Task<List<AssignmentDTO>> GetByUser(string userSub, CancellationToken ct = default);
    Task<bool> Cancel(string disasterSlug, string userSub, CancellationToken ct = default);
    Task<int> CountActiveByDisaster(string disasterSlug, CancellationToken ct = default);
}
