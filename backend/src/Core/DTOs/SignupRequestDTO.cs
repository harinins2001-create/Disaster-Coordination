using Microsoft.AspNetCore.Http;

namespace Core.DTOs;

public class SignupRequestDTO
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nic { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Dob { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string PhotoKey { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public List<string> TravelMethods { get; set; } = new();
    public List<string> Roles { get; set; } = new();

    public IFormFile? Photo { get; set; }
}
