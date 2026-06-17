namespace Core.DTOs;

public class AssignmentDTO
{
    public string DisasterSlug { get; set; } = string.Empty;
    public string UserSub { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserPhotoKey { get; set; } = string.Empty;
    public string? UserPhotoUrl { get; set; }
    public string Status { get; set; } = "pledged";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
