using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IHumanAgentRequestRepository
{
    Task<HumanAgentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HumanAgentRequest?> GetOpenBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<HumanAgentRequest?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HumanAgentRequest>> GetByStatusAsync(HumanAgentRequestStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HumanAgentRequest>> GetAssignedToAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task AddAsync(HumanAgentRequest request, CancellationToken cancellationToken = default);
}
