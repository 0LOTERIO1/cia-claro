using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class AccessibilityPreferencesRepository : IAccessibilityPreferencesRepository
{
    private readonly AppDbContext _db;

    public AccessibilityPreferencesRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<AccessibilityPreferences?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _db.AccessibilityPreferences.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(AccessibilityPreferences preferences, CancellationToken cancellationToken = default)
    {
        await _db.AccessibilityPreferences.AddAsync(preferences, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
