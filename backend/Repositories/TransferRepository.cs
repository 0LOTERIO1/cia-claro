using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly AppDbContext _db;

    public TransferRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DepartmentTransfer>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _db.DepartmentTransfers
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DepartmentTransfer transfer, CancellationToken cancellationToken = default)
    {
        await _db.DepartmentTransfers.AddAsync(transfer, cancellationToken);
    }
}
