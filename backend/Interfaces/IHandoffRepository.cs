using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IHandoffRepository
{
    Task<Handoff?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(Handoff handoff, CancellationToken cancellationToken = default);
}
