using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class Disaster : _DisastersTable
{
    [DynamoDbProperty("entity")] public new string Entity { get; set; } = "disaster";

    [DynamoDbProperty("slug")] public string Slug { get; set; } = string.Empty;

    [DynamoDbProperty("title")] public string Title { get; set; } = string.Empty;

    [DynamoDbProperty("description")] public string Description { get; set; } = string.Empty;

    [DynamoDbProperty("severity")] public string Severity { get; set; } = string.Empty;

    [DynamoDbProperty("location")] public string Location { get; set; } = string.Empty;

    [DynamoDbProperty("status")] public string Status { get; set; } = string.Empty;

    [DynamoDbProperty("reportedBy")] public string ReportedBy { get; set; } = string.Empty;

    [DynamoDbProperty("reportedBySub")] public string ReportedBySub { get; set; } = string.Empty;

    [DynamoDbProperty("reportedByName")] public string ReportedByName { get; set; } = string.Empty;

    [DynamoDbProperty("requiredVolunteers")] public int RequiredVolunteers { get; set; }

    [DynamoDbProperty("requiredResources")] public List<RequiredResource> RequiredResources { get; set; } = new();

    [DynamoDbProperty("photoKeys")] public List<string> PhotoKeys { get; set; } = new();

    [DynamoDbProperty("rejectionReason")] public string RejectionReason { get; set; } = string.Empty;
}

public class RequiredResource
{
    [DynamoDbProperty("itemType")] public string ItemType { get; set; } = string.Empty;

    [DynamoDbProperty("quantity")] public int Quantity { get; set; }
}
