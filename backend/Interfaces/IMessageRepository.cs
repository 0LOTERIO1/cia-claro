using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IMessageRepository
{
    Task<IReadOnlyList<Message>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(Message message, CancellationToken cancellationToken = default);
}
