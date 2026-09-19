using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class TwoFactorChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TwoFactorChallengePurpose Purpose { get; set; }
    public string? PendingSecretEncrypted { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
