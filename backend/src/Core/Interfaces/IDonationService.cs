using Core.DTOs;

namespace Core.Interfaces;

public interface IDonationService
{
    Task<DonationDTO?> Create(DonationDTO dto, CancellationToken ct = default);
    Task<List<DonationDTO>> GetByDisaster(string disasterSlug, CancellationToken ct = default);
    Task<List<DonationDTO>> GetByUser(string userSub, CancellationToken ct = default);
}
