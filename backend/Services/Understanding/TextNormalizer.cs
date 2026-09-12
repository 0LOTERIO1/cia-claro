using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cia.Api.Services.Understanding;

public static class TextNormalizer
{
    private static readonly Regex ExtraSpaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly (Regex Pattern, string Replacement)[] Abbreviations =
    {
        (new Regex(@"\bn\b", RegexOptions.Compiled), "nao"),
        (new Regex(@"\bss\b", RegexOptions.Compiled), "sim"),
        (new Regex(@"\bblz\b", RegexOptions.Compiled), "beleza"),
        (new Regex(@"\bvc\b", RegexOptions.Compiled), "voce"),
        (new Regex(@"\bpq\b", RegexOptions.Compiled), "porque"),
        (new Regex(@"\btb\b", RegexOptions.Compiled), "tambem"),
        (new Regex(@"\btd\b", RegexOptions.Compiled), "tudo"),
        (new Regex(@"\bpode ser\b", RegexOptions.Compiled), "pode ser"),
        (new Regex(@"\bwi-fi\b", RegexOptions.Compiled), "wifi"),
        (new Regex(@"\bwi fi\b", RegexOptions.Compiled), "wifi"),
        (new Regex(@"\bnet\b", RegexOptions.Compiled), "internet")
    };

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var value = RemoveDiacritics(text.Trim().ToLowerInvariant());
        value = ExtraSpaces.Replace(value, " ");
        foreach (var (pattern, replacement) in Abbreviations)
        {
            value = pattern.Replace(value, replacement);
        }

        return value.Trim();
    }

    public static bool EqualsAny(string normalized, params string[] values)
    {
        return values.Any(value => string.Equals(normalized, value, StringComparison.Ordinal));
    }

    public static bool ContainsAny(string normalized, params string[] terms)
    {
        return terms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static string RemoveDiacritics(string text)
    {
        var form = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
