namespace Core.DTOs;

public class DonationDTO
{
    public string Id { get; set; } = string.Empty;
    public string DisasterSlug { get; set; } = string.Empty;
    public string UserSub { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
