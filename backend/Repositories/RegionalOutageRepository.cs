using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class RegionalOutageRepository : IRegionalOutageRepository
{
    private readonly AppDbContext _db;

    public RegionalOutageRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RegionalOutage>> ListAsync(
        bool includeResolved,
        CancellationToken cancellationToken = default)
    {
        var query = _db.RegionalOutages.AsNoTracking();
        if (!includeResolved)
        {
            query = query.Where(outage => outage.ResolvedAt == null);
        }

        return await query
            .OrderBy(outage => outage.ResolvedAt != null)
            .ThenByDescending(outage => outage.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegionalOutage>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.RegionalOutages
            .AsNoTracking()
            .Where(outage => outage.ResolvedAt == null)
            .OrderByDescending(outage => outage.PostalCodePrefix.Length)
            .ThenByDescending(outage => outage.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<RegionalOutage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.RegionalOutages.FirstOrDefaultAsync(outage => outage.Id == id, cancellationToken);
    }

    public Task AddAsync(RegionalOutage outage, CancellationToken cancellationToken = default)
    {
        return _db.RegionalOutages.AddAsync(outage, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
