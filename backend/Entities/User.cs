using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerId { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecretEncrypted { get; set; }
    public DateTime? TwoFactorEnabledAt { get; set; }
    public long? LastTotpTimeStep { get; set; }

    public Customer? Customer { get; set; }
    public AccessibilityPreferences? AccessibilityPreferences { get; set; }
    public ICollection<HumanAgentRequest> AssignedRequests { get; set; } = new List<HumanAgentRequest>();
    public ICollection<TwoFactorChallenge> TwoFactorChallenges { get; set; } = new List<TwoFactorChallenge>();
    public ICollection<TwoFactorRecoveryCode> TwoFactorRecoveryCodes { get; set; } = new List<TwoFactorRecoveryCode>();
}
