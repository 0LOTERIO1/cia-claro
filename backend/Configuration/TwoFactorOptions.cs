namespace Cia.Api.Configuration;

public class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    public string Issuer { get; set; } = "CIA Claro";
    public string EncryptionKey { get; set; } = string.Empty;
    public int ChallengeMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int RecoveryCodeCount { get; set; } = 10;
}
