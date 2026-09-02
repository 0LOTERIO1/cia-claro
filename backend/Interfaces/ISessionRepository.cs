using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface ISessionRepository
{
    Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ConversationSession?> GetActiveByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<ConversationSession?> GetOpenByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSession>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSession>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CountCreatedOnDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
