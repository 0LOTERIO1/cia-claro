using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class ChannelIdentityRepository : IChannelIdentityRepository
{
    private readonly AppDbContext _db;

    public ChannelIdentityRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<CustomerChannelIdentity?> GetByChannelUserAsync(
        ChannelType channel,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        return _db.CustomerChannelIdentities
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Channel == channel && x.ExternalUserId == externalUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerChannelIdentity>> GetByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CustomerChannelIdentities
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CustomerChannelIdentity identity, CancellationToken cancellationToken = default)
    {
        await _db.CustomerChannelIdentities.AddAsync(identity, cancellationToken);
    }

    public void Remove(CustomerChannelIdentity identity)
    {
        _db.CustomerChannelIdentities.Remove(identity);
    }
}

public class ChannelLinkCodeRepository : IChannelLinkCodeRepository
{
    private readonly AppDbContext _db;

    public ChannelLinkCodeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ChannelLinkCode>> GetPendingAsync(
        string customerId,
        ChannelType channel,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _db.ChannelLinkCodes
            .Where(x => x.CustomerId == customerId
                        && x.Channel == channel
                        && x.UsedAt == null
                        && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public Task<ChannelLinkCode?> GetLatestByHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        return _db.ChannelLinkCodes
            .Include(x => x.Customer)
            .Where(x => x.CodeHash == codeHash)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(ChannelLinkCode code, CancellationToken cancellationToken = default)
    {
        await _db.ChannelLinkCodes.AddAsync(code, cancellationToken);
    }
}
