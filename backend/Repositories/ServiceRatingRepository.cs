using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class ServiceRatingRepository : IServiceRatingRepository
{
    private readonly AppDbContext _db;

    public ServiceRatingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ServiceRating?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _db.ServiceRatings.FirstOrDefaultAsync(r => r.SessionId == sessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRating>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ServiceRatings
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ServiceRating rating, CancellationToken cancellationToken = default)
    {
        await _db.ServiceRatings.AddAsync(rating, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
