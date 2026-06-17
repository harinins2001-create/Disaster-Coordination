using EfficientDynamoDb.Attributes;

namespace Core.Infrastructure.Persistence.Models;

public class User : _UsersTable
{
    [DynamoDbProperty("entity")] public new string Entity { get; set; } = "user";

    [DynamoDbProperty("sub")] public string Sub { get; set; } = string.Empty;

    [DynamoDbProperty("email")] public string Email { get; set; } = string.Empty;

    [DynamoDbProperty("name")] public string Name { get; set; } = string.Empty;

    [DynamoDbProperty("nic")] public string Nic { get; set; } = string.Empty;

    [DynamoDbProperty("phone")] public string Phone { get; set; } = string.Empty;

    [DynamoDbProperty("dob")] public string Dob { get; set; } = string.Empty;

    [DynamoDbProperty("gender")] public string Gender { get; set; } = string.Empty;

    [DynamoDbProperty("photoKey")] public string PhotoKey { get; set; } = string.Empty;

    [DynamoDbProperty("area")] public string Area { get; set; } = string.Empty;

    [DynamoDbProperty("skills")] public List<string> Skills { get; set; } = new();

    [DynamoDbProperty("travelMethods")] public List<string> TravelMethods { get; set; } = new();

    [DynamoDbProperty("roles")] public List<string> Roles { get; set; } = new();

    [DynamoDbProperty("active")] public bool Active { get; set; } = true;
}
