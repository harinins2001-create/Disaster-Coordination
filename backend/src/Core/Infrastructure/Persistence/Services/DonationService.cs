using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using EfficientDynamoDb;
using EfficientDynamoDb.Extensions;
using EDBDonation = Core.Infrastructure.Persistence.Models.Donation;
using DonationsTable = Core.Infrastructure.Persistence.Models._DonationsTable;

namespace Core.Infrastructure.Persistence.Services;

public class DonationService : IDonationService
{
    private readonly DynamoDbContext _context;
    private readonly IMapper _mapper;
    private readonly Keys _keys;
    private readonly string _tableName;

    public DonationService(DynamoDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
        _keys = new Keys();
        _tableName = Environment.GetEnvironmentVariable("DONATIONS_TABLE")
                     ?? throw new Exception("DONATIONS_TABLE env var not set");
    }

    public async Task<DonationDTO?> Create(DonationDTO dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
            dto.Id = Guid.NewGuid().ToString("N");

        dto.CreatedAt ??= DateTime.UtcNow;

        var (pk, sk) = _keys.GenerateDonationKeys(dto.DisasterSlug, dto.Id);
        var entity = _mapper.Map<DonationDTO, EDBDonation>(dto);
        entity.Pk = pk;
        entity.Sk = sk;

        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        var fetched = await _context.GetItem<EDBDonation>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        return fetched is null ? null : _mapper.Map<EDBDonation, DonationDTO>(fetched);
    }

    public async Task<List<DonationDTO>> GetByDisaster(string disasterSlug, CancellationToken ct = default)
    {
        var pk = _keys.DonationPartitionKey(disasterSlug);
        var page = await _context.Query<EDBDonation>()
            .WithTableName(_tableName)
            .WithKeyExpression(c => c.On(x => x.Pk).EqualTo(pk))
            .ToListAsync(ct);

        return page.Select(e => _mapper.Map<EDBDonation, DonationDTO>(e)).ToList();
    }

    public async Task<List<DonationDTO>> GetByUser(string userSub, CancellationToken ct = default)
    {
        var page = await _context.Query<EDBDonation>()
            .WithTableName(_tableName)
            .FromIndex(DonationsTable.Indexes.UserIndex)
            .WithKeyExpression(c => c.On(x => x.UserSub).EqualTo(userSub))
            .ToListAsync(ct);

        return page.Select(e => _mapper.Map<EDBDonation, DonationDTO>(e)).ToList();
    }
}
