using System.Security.Cryptography;
using Cia.Api.Configuration;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Cia.Api.Services;

public class TwoFactorCodeService
{
    private const int SecretSize = 20;
    private readonly string _issuer;

    public TwoFactorCodeService(IOptions<TwoFactorOptions> options)
    {
        _issuer = options.Value.Issuer.Trim();
    }

    public string CreateSecret()
    {
        return Base32Encoding.ToString(RandomNumberGenerator.GetBytes(SecretSize));
    }

    public string CreateOtpAuthUri(string email, string base32Secret)
    {
        var label = Uri.EscapeDataString($"{_issuer}:{email}");
        return $"otpauth://totp/{label}?secret={Uri.EscapeDataString(base32Secret)}" +
               $"&issuer={Uri.EscapeDataString(_issuer)}&algorithm=SHA1&digits=6&period=30";
    }

    public bool Verify(string base32Secret, string code, DateTime now, out long matchedTimeStep)
    {
        matchedTimeStep = -1;
        var normalizedCode = new string(code.Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != 6)
        {
            return false;
        }

        var totp = new Totp(
            Base32Encoding.ToBytes(base32Secret),
            step: 30,
            mode: OtpHashMode.Sha1,
            totpSize: 6);

        return totp.VerifyTotp(
            now,
            normalizedCode,
            out matchedTimeStep,
            VerificationWindow.RfcSpecifiedNetworkDelay);
    }
}
