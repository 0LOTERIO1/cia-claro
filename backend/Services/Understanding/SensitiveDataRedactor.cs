using System.Text.RegularExpressions;

namespace Cia.Api.Services.Understanding;

public sealed class SensitiveDataRedactor
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex CpfPattern = new(
        @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex CnpjPattern = new(
        @"(?<!\d)\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?!\d)",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?:\+?55[\s.-]?)?\(?\d{2}\)?[\s.-]?9?\d{4}[\s.-]?\d{4}(?!\d)",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex CredentialPattern = new(
        @"\b(?:senha|password|token|api[\s_-]?key|chave da api|cvv|cvc|codigo de seguranca)\b\s*[:=]?\s*\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex JwtPattern = new(
        @"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex CardCandidatePattern = new(
        @"(?<!\d)(?:\d[\s-]?){13,19}(?!\d)",
        RegexOptions.Compiled,
        RegexTimeout);

    public string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value?.Trim() ?? string.Empty;
        }

        var redacted = value.Trim();
        redacted = JwtPattern.Replace(redacted, "[TOKEN_REMOVIDO]");
        redacted = CredentialPattern.Replace(redacted, "[CREDENCIAL_REMOVIDA]");
        redacted = EmailPattern.Replace(redacted, "[EMAIL_REMOVIDO]");
        redacted = CnpjPattern.Replace(redacted, "[CNPJ_REMOVIDO]");
        redacted = CpfPattern.Replace(redacted, "[CPF_REMOVIDO]");
        redacted = CardCandidatePattern.Replace(redacted, match =>
            IsValidPaymentCard(match.Value) ? "[CARTAO_REMOVIDO]" : match.Value);
        redacted = PhonePattern.Replace(redacted, "[TELEFONE_REMOVIDO]");
        return redacted;
    }

    public bool ContainsSensitiveData(string? value)
    {
        return !string.Equals(value?.Trim() ?? string.Empty, Redact(value), StringComparison.Ordinal);
    }

    private static bool IsValidPaymentCard(string candidate)
    {
        var digits = candidate.Where(char.IsDigit).Select(character => character - '0').ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var digit = digits[index];
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
