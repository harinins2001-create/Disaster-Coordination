namespace Core.DTOs;

public class DisasterDTO
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string ReportedBySub { get; set; } = string.Empty;
    public string ReportedByName { get; set; } = string.Empty;
    public int RequiredVolunteers { get; set; }
    public List<RequiredResourceDTO>? RequiredResources { get; set; }
    public List<string>? PhotoKeys { get; set; }
    public List<string> PhotoUrls { get; set; } = new();
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RequiredResourceDTO
{
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
