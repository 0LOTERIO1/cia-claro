namespace Cia.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "CIA-academic-demo-signing-key-change-me!";
    public string Issuer { get; set; } = "CIA";
    public string Audience { get; set; } = "CIA";
    public int ExpiresHours { get; set; } = 12;
}
