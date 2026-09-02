using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class HumanAgentRequestRepository : IHumanAgentRequestRepository
{
    private readonly AppDbContext _db;

    public HumanAgentRequestRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<HumanAgentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Query()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<HumanAgentRequest?> GetOpenBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(r => r.SessionId == sessionId &&
                        (r.Status == HumanAgentRequestStatus.Waiting ||
                         r.Status == HumanAgentRequestStatus.Assigned))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<HumanAgentRequest?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HumanAgentRequest>> GetByStatusAsync(
        HumanAgentRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HumanAgentRequest>> GetAssignedToAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(r => r.AssignedAgentId == agentId && r.Status == HumanAgentRequestStatus.Assigned)
            .OrderByDescending(r => r.AssignedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(HumanAgentRequest request, CancellationToken cancellationToken = default)
    {
        await _db.HumanAgentRequests.AddAsync(request, cancellationToken);
    }

    private IQueryable<HumanAgentRequest> Query()
    {
        return _db.HumanAgentRequests
            .Include(r => r.AssignedAgent)
            .Include(r => r.Session)
                .ThenInclude(s => s.Customer)
            .Include(r => r.Session)
                .ThenInclude(s => s.Context)
            .Include(r => r.Session)
                .ThenInclude(s => s.Transfers);
    }
}
