using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class Assignment : _AssignmentsTable
{
    [DynamoDbProperty("entity")] public new string Entity { get; set; } = "assignment";

    [DynamoDbProperty("disasterSlug")] public string DisasterSlug { get; set; } = string.Empty;

    [DynamoDbProperty("userSub")] public string UserSub { get; set; } = string.Empty;

    [DynamoDbProperty("userName")] public string UserName { get; set; } = string.Empty;

    [DynamoDbProperty("userEmail")] public string UserEmail { get; set; } = string.Empty;

    [DynamoDbProperty("userPhotoKey")] public string UserPhotoKey { get; set; } = string.Empty;

    [DynamoDbProperty("status")] public string Status { get; set; } = "pledged";
}
