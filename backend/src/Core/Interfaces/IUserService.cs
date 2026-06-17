using Core.DTOs;

namespace Core.Interfaces;

public interface IUserService
{
    Task<UserDTO?> SignupPublic(SignupRequestDTO request, CancellationToken ct = default);
    Task<UserDTO?> GetBySub(string sub, CancellationToken ct = default);
    Task<UserDTO?> GetByEmail(string email, CancellationToken ct = default);
    Task<UserDTO?> GetByNic(string nic, CancellationToken ct = default);
    Task<List<UserDTO>> ListUsers(string? role, string? district, string? search, CancellationToken ct = default);
    Task<UserDTO?> UpdateProfile(string sub, UserDTO patch, CancellationToken ct = default);
    Task<UserDTO?> SetRoles(string sub, List<string> roles, CancellationToken ct = default);
    Task<UserDTO?> SetActive(string sub, bool active, CancellationToken ct = default);
    Task<UserDTO?> AdminCreate(SignupRequestDTO request, CancellationToken ct = default);
}
