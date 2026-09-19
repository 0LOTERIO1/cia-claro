using System.Security.Cryptography;
using System.Text;
using Cia.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class TwoFactorProtector
{
    private const int KeySize = 32;
    private readonly byte[] _key;

    public TwoFactorProtector(IOptions<TwoFactorOptions> options)
    {
        try
        {
            _key = Convert.FromBase64String(options.Value.EncryptionKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "TwoFactor:EncryptionKey deve ser uma chave Base64 de 32 bytes.", exception);
        }

        if (_key.Length != KeySize)
        {
            throw new InvalidOperationException(
                "TwoFactor:EncryptionKey deve ser uma chave Base64 de 32 bytes.");
        }
    }

    public string Protect(string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return string.Join(
            '.',
            "v1",
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    public string Unprotect(string protectedValue)
    {
        var parts = protectedValue.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "v1")
        {
            throw new CryptographicException("Segredo 2FA armazenado em formato inválido.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var ciphertext = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string HashRecoveryCode(string code)
    {
        using var hmac = new HMACSHA256(_key);
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes($"recovery:{NormalizeRecoveryCode(code)}"));
        return Convert.ToHexString(digest);
    }

    public static string CreateRecoveryCode()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        return string.Join('-', Enumerable.Range(0, 4).Select(index => raw.Substring(index * 5, 5)));
    }

    public static string NormalizeRecoveryCode(string code)
    {
        return new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
