using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface ITransferRepository
{
    Task<IReadOnlyList<DepartmentTransfer>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(DepartmentTransfer transfer, CancellationToken cancellationToken = default);
}
