namespace Core.Helpers;

// Marker class used to find the Core assembly for AutoMapper + service
// auto-registration (app assembly is not directly referenceable in .NET
// Core DI scenarios).
public class MappingAssemblyMarker
{
    public static readonly string EfficientDynamoDbTargetNamespace = "Core.Infrastructure.Persistence.Services";
}
