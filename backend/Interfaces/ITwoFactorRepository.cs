using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface ITwoFactorRepository
{
    Task AddChallengeAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default);
    Task<TwoFactorChallenge?> GetChallengeAsync(Guid id, CancellationToken cancellationToken = default);
    Task InvalidateActiveChallengesAsync(Guid userId, DateTime consumedAt, CancellationToken cancellationToken = default);
    Task<TwoFactorRecoveryCode?> GetRecoveryCodeAsync(
        Guid userId,
        string codeHash,
        CancellationToken cancellationToken = default);
    Task ReplaceRecoveryCodesAsync(
        Guid userId,
        IReadOnlyCollection<TwoFactorRecoveryCode> recoveryCodes,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
