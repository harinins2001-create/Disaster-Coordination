using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using EfficientDynamoDb;
using EfficientDynamoDb.Extensions;
using EDBResource = Core.Infrastructure.Persistence.Models.Resource;

namespace Core.Infrastructure.Persistence.Services;

public class ResourceService : IResourceService
{
    private readonly DynamoDbContext _context;
    private readonly IMapper _mapper;
    private readonly Keys _keys;
    private readonly string _tableName;

    public ResourceService(DynamoDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
        _keys = new Keys();
        _tableName = Environment.GetEnvironmentVariable("RESOURCES_TABLE")
                     ?? throw new Exception("RESOURCES_TABLE env var not set");
    }

    public async Task<List<ResourceDTO>> GetResourcesByDisaster(string disasterSlug, CancellationToken ct = default)
    {
        var pk = _keys.ResourcePartitionKey(disasterSlug);

        var entities = await _context.Query<EDBResource>()
            .WithTableName(_tableName)
            .WithKeyExpression(c => c.On(x => x.Pk).EqualTo(pk))
            .ToListAsync(ct);

        return entities.Select(e => _mapper.Map<EDBResource, ResourceDTO>(e)).ToList();
    }

    public async Task<ResourceDTO?> UpsertResource(string disasterSlug, string itemType, int quantity, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateResourceKeys(disasterSlug, itemType);

        var existing = await _context.GetItem<EDBResource>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        var nowDto = new ResourceDTO
        {
            DisasterSlug = disasterSlug,
            ItemType = itemType,
            Quantity = Math.Max(0, quantity),
            CreatedAt = existing is null ? DateTime.UtcNow : _Helpers_TryParse(existing.CreatedAt),
            UpdatedAt = DateTime.UtcNow
        };

        var entity = _mapper.Map<ResourceDTO, EDBResource>(nowDto);

        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        var fetched = await _context.GetItem<EDBResource>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        return fetched is null ? null : _mapper.Map<EDBResource, ResourceDTO>(fetched);
    }

    public async Task<bool> DeleteResource(string disasterSlug, string itemType, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateResourceKeys(disasterSlug, itemType);

        await _context.DeleteItem<EDBResource>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return true;
    }

    private static DateTime? _Helpers_TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }
}
