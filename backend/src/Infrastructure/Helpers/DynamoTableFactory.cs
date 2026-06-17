using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;

namespace Infrastructure.Helpers;

public record GsiDef(
    string Name,
    string PartitionKey,
    string? SortKey = null,
    ProjectionType Projection = ProjectionType.ALL);

public record TableDef(
    string Name,
    string PartitionKey,
    string? SortKey = null,
    GsiDef[]? Gsis = null,
    BillingMode Billing = BillingMode.PAY_PER_REQUEST,
    RemovalPolicy Removal = RemovalPolicy.RETAIN);

public static class DynamoTableFactory
{
    public static Table Create(Construct scope, TableDef def)
    {
        var props = new TableProps
        {
            TableName = def.Name,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = def.PartitionKey,
                Type = AttributeType.STRING
            },
            BillingMode = def.Billing,
            RemovalPolicy = def.Removal
        };

        if (!string.IsNullOrEmpty(def.SortKey))
        {
            props.SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = def.SortKey,
                Type = AttributeType.STRING
            };
        }

        var table = new Table(scope, $"Table-{def.Name}", props);

        if (def.Gsis is not null)
        {
            foreach (var gsi in def.Gsis)
            {
                var gsiProps = new GlobalSecondaryIndexProps
                {
                    IndexName = gsi.Name,
                    PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
                    {
                        Name = gsi.PartitionKey,
                        Type = AttributeType.STRING
                    },
                    ProjectionType = gsi.Projection
                };

                if (!string.IsNullOrEmpty(gsi.SortKey))
                {
                    gsiProps.SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
                    {
                        Name = gsi.SortKey,
                        Type = AttributeType.STRING
                    };
                }

                table.AddGlobalSecondaryIndex(gsiProps);
            }
        }

        return table;
    }
}
