using System.Reflection;
using Amazon.Runtime;
using Core.Helpers;
using EfficientDynamoDb;
using EfficientDynamoDb.Configs;
using EfficientDynamoDb.Credentials.AWSSDK;
using Microsoft.Extensions.DependencyInjection;
using EfficientRegionEndpoint = EfficientDynamoDb.Configs.RegionEndpoint;

namespace Core.Extensions;

public static class ServiceExtensions
{
    // Singleton: persistent connection pool
    public static IServiceCollection AddEfficientDynamoDb(this IServiceCollection services)
    {
        services.AddSingleton<DynamoDbContext>(_ =>
        {
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "local";
            var isLocal = environment.Equals("local", StringComparison.OrdinalIgnoreCase);

            IAwsCredentialsProvider credentialsProvider;
            var region = EfficientRegionEndpoint.APSouth1;

            if (isLocal)
            {
                var localUrl = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_ENDPOINT")
                               ?? "http://localhost:8000";

                region = EfficientRegionEndpoint.Create("local", localUrl);
                var dummy = new BasicAWSCredentials("dummy", "dummy");
                credentialsProvider = dummy.ToCredentialsProvider();
            }
            else
            {
#pragma warning disable CS0618
                var awsSdkCredentials = FallbackCredentialsFactory.GetCredentials();
#pragma warning restore CS0618
                credentialsProvider = awsSdkCredentials.ToCredentialsProvider();
            }

            var config = new DynamoDbContextConfig(region, credentialsProvider);
            return new DynamoDbContext(config);
        });

        return services;
    }

    // Scoped: auto-discover Core.Infrastructure.Persistence.Services, register each as IFooService -> FooService
    public static IServiceCollection RegisterDataServices(this IServiceCollection services)
    {
        var assembly = typeof(MappingAssemblyMarker).Assembly;

        services.AddAutoMapper(new[] { assembly });

        var serviceTypes = assembly.GetTypes()
            .Where(t => t.Namespace == MappingAssemblyMarker.EfficientDynamoDbTargetNamespace &&
                        !t.IsAbstract &&
                        !t.IsInterface)
            .ToList();

        foreach (var type in serviceTypes)
        {
            var iface = type.GetInterfaces().FirstOrDefault(i => i.Name == $"I{type.Name}");
            if (iface is not null)
            {
                services.AddScoped(iface, type);
            }
            else
            {
                services.AddScoped(type);
            }
        }

        return services;
    }
}
