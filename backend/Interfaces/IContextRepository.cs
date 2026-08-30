using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IContextRepository
{
    Task<ConversationContext?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(ConversationContext context, CancellationToken cancellationToken = default);
}
