using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class HandoffRepository : IHandoffRepository
{
    private readonly AppDbContext _db;

    public HandoffRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Handoff?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _db.Handoffs
            .Where(h => h.SessionId == sessionId)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Handoff handoff, CancellationToken cancellationToken = default)
    {
        await _db.Handoffs.AddAsync(handoff, cancellationToken);
    }
}
