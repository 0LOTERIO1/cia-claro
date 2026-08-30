using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class ContextRepository : IContextRepository
{
    private readonly AppDbContext _db;

    public ContextRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ConversationContext?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _db.ConversationContexts.FirstOrDefaultAsync(c => c.SessionId == sessionId, cancellationToken);
    }

    public async Task AddAsync(ConversationContext context, CancellationToken cancellationToken = default)
    {
        await _db.ConversationContexts.AddAsync(context, cancellationToken);
    }
}
