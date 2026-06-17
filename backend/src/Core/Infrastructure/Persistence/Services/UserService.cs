using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using EfficientDynamoDb;
using EfficientDynamoDb.Extensions;
using EfficientDynamoDb.Operations.Query;
using EDBUser = Core.Infrastructure.Persistence.Models.User;
using UsersTable = Core.Infrastructure.Persistence.Models._UsersTable;

namespace Core.Infrastructure.Persistence.Services;

public class UserService : IUserService
{
    private static readonly HashSet<string> SelfAssignableRoles =
        new(StringComparer.OrdinalIgnoreCase) { "helper" };

    private static readonly HashSet<string> AllRoles =
        new(StringComparer.OrdinalIgnoreCase) { "admin", "moderator", "medic", "helper" };

    private readonly DynamoDbContext _context;
    private readonly IMapper _mapper;
    private readonly IS3Service _s3;
    private readonly Keys _keys;
    private readonly string _tableName;
    private readonly string _userPoolId;

    public UserService(DynamoDbContext context, IMapper mapper, IS3Service s3)
    {
        _context = context;
        _mapper = mapper;
        _s3 = s3;
        _keys = new Keys();
        _tableName = Environment.GetEnvironmentVariable("USERS_TABLE")
                     ?? throw new Exception("USERS_TABLE env var not set");
        _userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID")
                      ?? throw new Exception("COGNITO_USER_POOL_ID env var not set");
    }

    public async Task<UserDTO?> SignupPublic(SignupRequestDTO request, CancellationToken ct = default)
    {
        var filtered = (request.Roles ?? new List<string>())
            .Where(r => SelfAssignableRoles.Contains(r))
            .Select(r => r.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (filtered.Count == 0) filtered.Add("helper");

        request.Roles = filtered;
        return await AdminCreate(request, ct);
    }

    public async Task<UserDTO?> AdminCreate(SignupRequestDTO request, CancellationToken ct = default)
    {
        var existingByEmail = await GetByEmail(request.Email, ct);
        if (existingByEmail is not null) return null;

        if (!string.IsNullOrWhiteSpace(request.Nic))
        {
            var existingByNic = await GetByNic(request.Nic, ct);
            if (existingByNic is not null) return null;
        }

        using var cognito = new AmazonCognitoIdentityProviderClient(
            Amazon.RegionEndpoint.APSouth1);

        var createReq = new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = request.Email,
            MessageAction = MessageActionType.SUPPRESS,
            TemporaryPassword = request.Password,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "email", Value = request.Email },
                new() { Name = "email_verified", Value = "true" },
                new() { Name = "name", Value = request.Name }
            }
        };

        var created = await cognito.AdminCreateUserAsync(createReq, ct);
        var sub = created.User.Attributes.FirstOrDefault(a => a.Name == "sub")?.Value
                  ?? throw new Exception("Cognito did not return sub");

        await cognito.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = request.Email,
            Password = request.Password,
            Permanent = true
        }, ct);

        var photoKey = request.PhotoKey ?? string.Empty;
        if (request.Photo is not null)
        {
            photoKey = await _s3.UploadUserPhoto(sub, request.Photo, ct);
        }

        var roles = (request.Roles ?? new List<string>())
            .Where(r => AllRoles.Contains(r))
            .Select(r => r.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (roles.Count == 0) roles.Add("helper");

        var (pk, sk) = _keys.GenerateUserKeys(sub);
        var entity = new EDBUser
        {
            Pk = pk,
            Sk = sk,
            Entity = "user",
            Sub = sub,
            Email = request.Email,
            Name = request.Name,
            Nic = request.Nic,
            Phone = request.Phone,
            Dob = request.Dob,
            Gender = request.Gender,
            PhotoKey = photoKey,
            Area = request.Area,
            Skills = request.Skills ?? new List<string>(),
            TravelMethods = request.TravelMethods ?? new List<string>(),
            Roles = roles,
            Active = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = null
        };

        await _context.PutItem()
            .WithItem(entity)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetBySub(sub, ct);
    }

    private UserDTO Enrich(UserDTO dto)
    {
        dto.PhotoUrl = _s3.GetPresignedUrl(dto.PhotoKey);
        return dto;
    }

    public async Task<UserDTO?> GetBySub(string sub, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sub)) return null;
        var (pk, sk) = _keys.GenerateUserKeys(sub);

        var entity = await _context.GetItem<EDBUser>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);

        return entity is null ? null : Enrich(_mapper.Map<EDBUser, UserDTO>(entity));
    }

    public async Task<UserDTO?> GetByEmail(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var entities = await _context.Query<EDBUser>()
            .WithTableName(_tableName)
            .FromIndex(UsersTable.Indexes.EmailIndex)
            .WithKeyExpression(c => c.On(x => x.Email).EqualTo(email))
            .ToListAsync(ct);

        var match = entities.FirstOrDefault();
        return match is null ? null : Enrich(_mapper.Map<EDBUser, UserDTO>(match));
    }

    public async Task<UserDTO?> GetByNic(string nic, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nic)) return null;

        var entities = await _context.Query<EDBUser>()
            .WithTableName(_tableName)
            .FromIndex(UsersTable.Indexes.NicIndex)
            .WithKeyExpression(c => c.On(x => x.Nic).EqualTo(nic))
            .ToListAsync(ct);

        var match = entities.FirstOrDefault();
        return match is null ? null : Enrich(_mapper.Map<EDBUser, UserDTO>(match));
    }

    public async Task<List<UserDTO>> ListUsers(string? role, string? district, string? search, CancellationToken ct = default)
    {
        List<EDBUser> entities;

        if (!string.IsNullOrWhiteSpace(district))
        {
            var page = await _context.Query<EDBUser>()
                .WithTableName(_tableName)
                .FromIndex(UsersTable.Indexes.DistrictIndex)
                .WithKeyExpression(c => c.On(x => x.Area).EqualTo(district))
                .ToListAsync(ct);
            entities = page.ToList();
        }
        else
        {
            entities = new List<EDBUser>();
            var scan = _context.Scan<EDBUser>()
                .WithTableName(_tableName)
                .WithFilterExpression(c => c.On(x => x.Entity).EqualTo("user"))
                .ToAsyncEnumerable();
            await foreach (var item in scan.WithCancellation(ct))
            {
                entities.Add(item);
            }
        }

        IEnumerable<EDBUser> filtered = entities;

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.ToLowerInvariant();
            filtered = filtered.Where(u => u.Roles.Any(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            filtered = filtered.Where(u =>
                (u.Name ?? string.Empty).ToLowerInvariant().Contains(s) ||
                (u.Email ?? string.Empty).ToLowerInvariant().Contains(s) ||
                (u.Nic ?? string.Empty).ToLowerInvariant().Contains(s));
        }

        return filtered.Select(e => Enrich(_mapper.Map<EDBUser, UserDTO>(e))).ToList();
    }

    public async Task<UserDTO?> UpdateProfile(string sub, UserDTO patch, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateUserKeys(sub);

        var existing = await _context.GetItem<EDBUser>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);
        if (existing is null) return null;

        if (!string.IsNullOrWhiteSpace(patch.PhotoKey)) existing.PhotoKey = patch.PhotoKey;
        if (!string.IsNullOrWhiteSpace(patch.Area)) existing.Area = patch.Area;
        if (patch.Skills is not null && patch.Skills.Count > 0) existing.Skills = patch.Skills;
        if (patch.TravelMethods is not null && patch.TravelMethods.Count > 0) existing.TravelMethods = patch.TravelMethods;
        existing.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _context.PutItem()
            .WithItem(existing)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetBySub(sub, ct);
    }

    public async Task<UserDTO?> SetRoles(string sub, List<string> roles, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateUserKeys(sub);

        var existing = await _context.GetItem<EDBUser>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);
        if (existing is null) return null;

        var normalized = (roles ?? new List<string>())
            .Where(r => AllRoles.Contains(r))
            .Select(r => r.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (normalized.Count == 0) normalized.Add("helper");

        existing.Roles = normalized;
        existing.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _context.PutItem()
            .WithItem(existing)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        return await GetBySub(sub, ct);
    }

    public async Task<UserDTO?> SetActive(string sub, bool active, CancellationToken ct = default)
    {
        var (pk, sk) = _keys.GenerateUserKeys(sub);

        var existing = await _context.GetItem<EDBUser>()
            .WithPrimaryKey(pk, sk)
            .WithTableName(_tableName)
            .ToItemAsync(ct);
        if (existing is null) return null;

        existing.Active = active;
        existing.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _context.PutItem()
            .WithItem(existing)
            .WithTableName(_tableName)
            .ExecuteAsync(ct);

        using var cognito = new AmazonCognitoIdentityProviderClient(
            Amazon.RegionEndpoint.APSouth1);

        if (active)
        {
            await cognito.AdminEnableUserAsync(new AdminEnableUserRequest
            {
                UserPoolId = _userPoolId,
                Username = existing.Email
            }, ct);
        }
        else
        {
            await cognito.AdminDisableUserAsync(new AdminDisableUserRequest
            {
                UserPoolId = _userPoolId,
                Username = existing.Email
            }, ct);
        }

        return await GetBySub(sub, ct);
    }
}
