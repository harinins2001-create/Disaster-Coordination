using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class _DonationsTable
{
    [DynamoDbProperty("PK", DynamoDbAttributeType.PartitionKey)]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbProperty("SK", DynamoDbAttributeType.SortKey)]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbProperty("entity")] public string Entity { get; set; } = string.Empty;

    [DynamoDbProperty("createdAt")] public string CreatedAt { get; set; } = string.Empty;

    [DynamoDbProperty("updatedAt")] public string? UpdatedAt { get; set; }

    public static class Indexes
    {
        public const string UserIndex = "user-index";
    }
}
