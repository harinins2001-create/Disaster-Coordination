using AutoMapper;
using Core.DTOs;
using Core.Interfaces;
using Core.Helpers;
using EfficientDynamoDb;
using EfficientDynamoDb.Extensions;
using EDBDisaster = Core.Infrastructure.Persistence.Models.Disaster;
using DisastersTable = Core.Infrastructure.Persistence.Models._DisastersTable;

namespace Core.Infrastructure.Persistence.Services;

public class DisasterService : IDisasterService
{
    private readonly DynamoDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAssignmentService _assignments;
    private readonly IS3Service _s3;
    private readonly Keys _keys;
    private readonly string _tableName;

    public DisasterService(DynamoDbContext context, IMapper mapper, IAssignmentService assignments, IS3Service s3)
    {
        _context = context;
        _mapper = mapper;
        _assignments = assignments;
        _s3 = s3;
        _keys = new Keys();
        _tableName = Environment.GetEnvironmentVariable("DISASTERS_TABLE")
                     ?? throw new Exception("DISASTERS_TABLE env var not set");
    }

    private DisasterDTO Enrich(DisasterDTO dto)
    {
        dto.PhotoUrls = (dto.PhotoKeys ?? new List<string>())
            .Select(k => _s3.GetPresignedUrl(k))
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .ToList();
        return dto;
    }

    public async Task<List<DisasterDTO>> GetAllDisasters(CancellationToken ct = default)
    {
        var entities = await _context.Query<EDBDisaster>()
            .WithTableName(_tableName)
            .FromIndex(DisastersTable.Indexes.EntityIndex)
            .WithKeyExpression(c => c.On(x => x.Entity).EqualTo("disaster"))
            .ToListAsync(ct);

        return entities.Select(e => Enrich(_mapper.Map<EDBDisaster, DisasterDTO>(e))).ToList();
    }

    public async Task<List<DisasterDTO>> GetDisastersByStatus(string status, CancellationToken ct = default)
    {
        var all = await GetAllDisasters(ct);
        return all.Where(d => string.Equals(d.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<DisasterDTO>> GetDisastersBySubmitter(string sub, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sub)) return new List<DisasterDTO>();
        var all = await GetAllDisasters(ct);
        return all.Where(d => string.Equals(d.ReportedBySub, sub, StringComparison.Ordinal)).ToList();
    }

    public async Task<DisasterDTO?> SetStatus(string slug, string status, string? rejectionReason, CancellationToken ct = default)
    {
        var existing = await GetDisasterBySlug(slug, ct);
        if (existing is null) return null;

        existing.Status = status;
        if (rejectionReason is not null) existing.RejectionReason = rejectionReason;
        existing.UpdatedAt = DateTime.UtcNow;

        var entity = _mapper.Map<DisasterDTO, EDBDisaster>(existing);
        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetDisasterBySlug(slug, ct);
    }

    public async Task<DisasterDTO?> GetDisasterBySlug(string slug, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateDisasterKeys(slug);

        var entity = await _context.GetItem<EDBDisaster>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        return entity is null ? null : Enrich(_mapper.Map<EDBDisaster, DisasterDTO>(entity));
    }

    public async Task<DisasterDTO?> CreateDisaster(DisasterDTO dto, CancellationToken ct = default)
    {
        var baseSlug = string.IsNullOrWhiteSpace(dto.Slug)
            ? _keys.NameToSlug(dto.Title)
            : dto.Slug;
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = Guid.NewGuid().ToString("N")[..12];

        var candidate = baseSlug;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (await GetDisasterBySlug(candidate, ct) is null)
            {
                dto.Slug = candidate;
                dto.CreatedAt ??= DateTime.UtcNow;

                var entity = _mapper.Map<DisasterDTO, EDBDisaster>(dto);
                await _context.PutItem()
                    .WithItem(entity)
                    .WithTableName(_tableName)
                    .ExecuteAsync(ct);

                return await GetDisasterBySlug(dto.Slug, ct);
            }
            candidate = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";
        }
        return null;
    }

    public async Task<DisasterDTO?> UpdateDisaster(string slug, DisasterDTO dto, CancellationToken ct = default)
    {
        var existing = await GetDisasterBySlug(slug, ct);
        if (existing is null) return null;

        existing.Title = string.IsNullOrWhiteSpace(dto.Title) ? existing.Title : dto.Title;
        existing.Description = string.IsNullOrWhiteSpace(dto.Description) ? existing.Description : dto.Description;
        existing.Severity = string.IsNullOrWhiteSpace(dto.Severity) ? existing.Severity : dto.Severity;
        existing.Location = string.IsNullOrWhiteSpace(dto.Location) ? existing.Location : dto.Location;
        existing.Status = string.IsNullOrWhiteSpace(dto.Status) ? existing.Status : dto.Status;
        existing.ReportedBy = string.IsNullOrWhiteSpace(dto.ReportedBy) ? existing.ReportedBy : dto.ReportedBy;
        existing.ReportedBySub = string.IsNullOrWhiteSpace(dto.ReportedBySub) ? existing.ReportedBySub : dto.ReportedBySub;
        existing.ReportedByName = string.IsNullOrWhiteSpace(dto.ReportedByName) ? existing.ReportedByName : dto.ReportedByName;
        if (dto.RequiredVolunteers > 0) existing.RequiredVolunteers = dto.RequiredVolunteers;
        if (dto.RequiredResources is not null) existing.RequiredResources = dto.RequiredResources;
        if (dto.PhotoKeys is not null) existing.PhotoKeys = dto.PhotoKeys;
        if (dto.RejectionReason is not null) existing.RejectionReason = dto.RejectionReason;
        existing.UpdatedAt = DateTime.UtcNow;

        var entity = _mapper.Map<DisasterDTO, EDBDisaster>(existing);

        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetDisasterBySlug(slug, ct);
    }

    public async Task<DisasterDTO?> RecomputeNeedsMet(string slug, CancellationToken ct = default)
    {
        var existing = await GetDisasterBySlug(slug, ct);
        if (existing is null) return null;

        if (string.Equals(existing.Status, "closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(existing.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            return existing;

        var resourcesSvc = new ResourceService(_context, _mapper);
        var resources = await resourcesSvc.GetResourcesByDisaster(slug, ct);
        var resourceMap = resources.ToDictionary(r => r.ItemType, r => r.Quantity, StringComparer.OrdinalIgnoreCase);

        var volunteerCount = await _assignments.CountActiveByDisaster(slug, ct);

        var resourcesMet = (existing.RequiredResources?.Count ?? 0) == 0
            || existing.RequiredResources!.All(req =>
                resourceMap.TryGetValue(req.ItemType, out var have) && have >= req.Quantity);

        var volunteersMet = existing.RequiredVolunteers <= 0 || volunteerCount >= existing.RequiredVolunteers;

        var nextStatus = (resourcesMet && volunteersMet) ? "needs-met" : "active";

        if (!string.Equals(existing.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            existing.Status = nextStatus;
            existing.UpdatedAt = DateTime.UtcNow;
            var entity = _mapper.Map<DisasterDTO, EDBDisaster>(existing);
            await _context.PutItem()
                .WithItem(entity)
                .WithTableName(_tableName)
                .ExecuteAsync(ct);
        }

        return await GetDisasterBySlug(slug, ct);
    }

    public async Task<bool> DeleteDisaster(string slug, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateDisasterKeys(slug);

        await _context.DeleteItem<EDBDisaster>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return true;
    }
}
