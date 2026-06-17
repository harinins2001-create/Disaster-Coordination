namespace Core.DTOs;

public class UserDTO
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nic { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Dob { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string PhotoKey { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Area { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public List<string> TravelMethods { get; set; } = new();
    public List<string> Roles { get; set; } = new(); //helper, admin, medic, moderator
    public bool Active { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
