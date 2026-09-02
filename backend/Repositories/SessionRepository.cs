using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _db;

    public SessionRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.ConversationSessions
            .Include(s => s.Customer)
            .Include(s => s.Context)
            .Include(s => s.Transfers)
            .Include(s => s.HumanAgentRequests)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<ConversationSession?> GetActiveByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        return _db.ConversationSessions
            .Include(s => s.Customer)
            .Include(s => s.Context)
            .Include(s => s.Transfers)
            .Include(s => s.HumanAgentRequests)
            .Where(s => s.CustomerId == customerId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ConversationSession?> GetOpenByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        return _db.ConversationSessions
            .Include(s => s.Customer)
            .Include(s => s.Context)
            .Include(s => s.Transfers)
            .Include(s => s.HumanAgentRequests)
            .Where(s => s.CustomerId == customerId &&
                        (s.Status == SessionStatus.Active ||
                         s.Status == SessionStatus.WaitingForAgent ||
                         (s.Status == SessionStatus.Transferred &&
                          s.HumanAgentRequests.Any(r =>
                              r.Status == HumanAgentRequestStatus.Waiting ||
                              r.Status == HumanAgentRequestStatus.Assigned))))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSession>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        return await _db.ConversationSessions
            .Include(s => s.Customer)
            .Include(s => s.Context)
            .Include(s => s.Transfers)
            .Include(s => s.HumanAgentRequests)
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ConversationSessions
            .Include(s => s.Customer)
            .Include(s => s.Context)
            .Include(s => s.Transfers)
            .Include(s => s.HumanAgentRequests)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountCreatedOnDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = start.AddDays(1);

        return _db.ConversationSessions.CountAsync(
            s => s.CreatedAt >= start && s.CreatedAt < end,
            cancellationToken);
    }

    public async Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        await _db.ConversationSessions.AddAsync(session, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
