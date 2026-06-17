using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using EfficientDynamoDb;
using EfficientDynamoDb.Extensions;
using EDBAssignment = Core.Infrastructure.Persistence.Models.Assignment;
using AssignmentsTable = Core.Infrastructure.Persistence.Models._AssignmentsTable;

namespace Core.Infrastructure.Persistence.Services;

public class AssignmentService : IAssignmentService
{
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "pledged", "active", "done", "cancelled" };

    private readonly DynamoDbContext _context;
    private readonly IMapper _mapper;
    private readonly IS3Service _s3;
    private readonly Keys _keys;
    private readonly string _tableName;

    public AssignmentService(DynamoDbContext context, IMapper mapper, IS3Service s3)
    {
        _context = context;
        _mapper = mapper;
        _s3 = s3;
        _keys = new Keys();
        _tableName = Environment.GetEnvironmentVariable("ASSIGNMENTS_TABLE")
                     ?? throw new Exception("ASSIGNMENTS_TABLE env var not set");
    }

    private AssignmentDTO Enrich(AssignmentDTO dto)
    {
        dto.UserPhotoUrl = _s3.GetPresignedUrl(dto.UserPhotoKey);
        return dto;
    }

    public async Task<AssignmentDTO?> Pledge(string disasterSlug, string userSub, string userName, string userEmail, string userPhotoKey, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateAssignmentKeys(disasterSlug, userSub);

        var existing = await _context.GetItem<EDBAssignment>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        var entity = new EDBAssignment
        {
            Pk = pk,
            Sk = sk,
            Entity = "assignment",
            DisasterSlug = disasterSlug,
            UserSub = userSub,
            UserName = userName,
            UserEmail = userEmail,
            UserPhotoKey = userPhotoKey ?? string.Empty,
            Status = existing?.Status ?? "pledged",
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow.ToString("o"),
            UpdatedAt = existing is null ? null : DateTime.UtcNow.ToString("o")
        };

        if (existing is not null && string.Equals(existing.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = "pledged";
        }

        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetOne(disasterSlug, userSub, ct);
    }

    public async Task<AssignmentDTO?> SetStatus(string disasterSlug, string userSub, string status, CancellationToken ct = default)
    {
        if (!AllowedStatuses.Contains(status)) return null;

        var (pk, sk) = _keys.GenerateAssignmentKeys(disasterSlug, userSub);
        var existing = await _context.GetItem<EDBAssignment>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);
        if (existing is null) return null;

        existing.Status = status.ToLowerInvariant();
        existing.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _context.PutItem()
            .WithItem(existing)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetOne(disasterSlug, userSub, ct);
    }

    public async Task<AssignmentDTO?> GetOne(string disasterSlug, string userSub, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateAssignmentKeys(disasterSlug, userSub);
        var entity = await _context.GetItem<EDBAssignment>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        return entity is null ? null : Enrich(_mapper.Map<EDBAssignment, AssignmentDTO>(entity));
    }

    public async Task<List<AssignmentDTO>> GetByDisaster(string disasterSlug, CancellationToken ct = default)
    {
        var pk = _keys.AssignmentPartitionKey(disasterSlug);
        var page = await _context.Query<EDBAssignment>()
            .WithTableName(_tableName)
            .WithKeyExpression(c => c.On(x => x.Pk).EqualTo(pk))
            .ToListAsync(ct);

        return page.Select(e => Enrich(_mapper.Map<EDBAssignment, AssignmentDTO>(e))).ToList();
    }

    public async Task<List<AssignmentDTO>> GetByUser(string userSub, CancellationToken ct = default)
    {
        var page = await _context.Query<EDBAssignment>()
            .WithTableName(_tableName)
            .FromIndex(AssignmentsTable.Indexes.UserIndex)
            .WithKeyExpression(c => c.On(x => x.UserSub).EqualTo(userSub))
            .ToListAsync(ct);

        return page.Select(e => Enrich(_mapper.Map<EDBAssignment, AssignmentDTO>(e))).ToList();
    }

    public async Task<bool> Cancel(string disasterSlug, string userSub, CancellationToken ct = default)
    {
        var updated = await SetStatus(disasterSlug, userSub, "cancelled", ct);
        return updated is not null;
    }

    public async Task<int> CountActiveByDisaster(string disasterSlug, CancellationToken ct = default)
    {
        var all = await GetByDisaster(disasterSlug, ct);
        return all.Count(a =>
            string.Equals(a.Status, "pledged", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Status, "active", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Status, "done", StringComparison.OrdinalIgnoreCase));
    }
}
