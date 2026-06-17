using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class Donation : _DonationsTable
{
    [DynamoDbProperty("entity")] public new string Entity { get; set; } = "donation";

    [DynamoDbProperty("id")] public string Id { get; set; } = string.Empty;

    [DynamoDbProperty("disasterSlug")] public string DisasterSlug { get; set; } = string.Empty;

    [DynamoDbProperty("userSub")] public string UserSub { get; set; } = string.Empty;

    [DynamoDbProperty("userName")] public string UserName { get; set; } = string.Empty;

    [DynamoDbProperty("itemType")] public string ItemType { get; set; } = string.Empty;

    [DynamoDbProperty("quantity")] public int Quantity { get; set; }

    [DynamoDbProperty("note")] public string Note { get; set; } = string.Empty;
}
