using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class Resource : _ResourcesTable
{
    [DynamoDbProperty("entity")] public new string Entity { get; set; } = "resource";

    [DynamoDbProperty("disasterSlug")] public string DisasterSlug { get; set; } = string.Empty;

    [DynamoDbProperty("category")] public string ItemType { get; set; } = string.Empty;

    [DynamoDbProperty("quantity")] public int Quantity { get; set; }
}
