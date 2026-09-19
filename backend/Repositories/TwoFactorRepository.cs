using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Repositories;

public class TwoFactorRepository : ITwoFactorRepository
{
    private readonly AppDbContext _db;

    public TwoFactorRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddChallengeAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default)
    {
        return _db.TwoFactorChallenges.AddAsync(challenge, cancellationToken).AsTask();
    }

    public Task<TwoFactorChallenge?> GetChallengeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _db.TwoFactorChallenges
            .Include(challenge => challenge.User)
            .FirstOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);
    }

    public async Task InvalidateActiveChallengesAsync(
        Guid userId,
        DateTime consumedAt,
        CancellationToken cancellationToken = default)
    {
        var active = await _db.TwoFactorChallenges
            .Where(challenge => challenge.UserId == userId && challenge.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var challenge in active)
        {
            challenge.ConsumedAt = consumedAt;
        }
    }

    public Task<TwoFactorRecoveryCode?> GetRecoveryCodeAsync(
        Guid userId,
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        return _db.TwoFactorRecoveryCodes.FirstOrDefaultAsync(
            recoveryCode =>
                recoveryCode.UserId == userId &&
                recoveryCode.CodeHash == codeHash &&
                recoveryCode.UsedAt == null,
            cancellationToken);
    }

    public async Task ReplaceRecoveryCodesAsync(
        Guid userId,
        IReadOnlyCollection<TwoFactorRecoveryCode> recoveryCodes,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.TwoFactorRecoveryCodes
            .Where(code => code.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.TwoFactorRecoveryCodes.RemoveRange(existing);
        await _db.TwoFactorRecoveryCodes.AddRangeAsync(recoveryCodes, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
