using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cia.Api.Services.Understanding;

public sealed record PromptSecurityAssessment(
    bool Blocked,
    string SanitizedText,
    IReadOnlyList<string> Reasons);

public sealed class PromptSecurityService
{
    public const string SafeRefusal =
        "Não posso alterar minhas instruções internas, revelar configurações ou executar comandos. " +
        "Posso ajudar com conexão, equipamentos, cobrança ou atendimento humano.";

    private static readonly string[] InstructionOverrideTerms =
    {
        "ignore as instrucoes",
        "ignore todas as instrucoes",
        "ignore instrucoes anteriores",
        "ignore previous instructions",
        "disregard previous instructions",
        "desconsidere as instrucoes",
        "desconsidere todas as regras",
        "esqueca as instrucoes",
        "esqueca tudo acima",
        "nao siga as regras",
        "bypass das regras"
    };

    private static readonly string[] PromptDisclosureTerms =
    {
        "system prompt",
        "prompt do sistema",
        "prompt oculto",
        "instrucoes do sistema",
        "instrucoes internas",
        "mensagem de sistema",
        "developer message",
        "mensagem do desenvolvedor",
        "revele suas instrucoes",
        "mostre suas instrucoes",
        "imprima suas instrucoes"
    };

    private static readonly string[] RoleSpoofingTerms =
    {
        "<|system|>",
        "<|assistant|>",
        "[system]",
        "[assistant]",
        "role: system",
        "role=system",
        "### system",
        "inicio das instrucoes do sistema",
        "begin system prompt"
    };

    private static readonly string[] JailbreakTerms =
    {
        "jailbreak",
        "modo desenvolvedor",
        "developer mode",
        "aja como dan",
        "act as dan",
        "sem restricoes",
        "sem limitacoes",
        "finja que nao existem regras",
        "voce agora e um modelo",
        "you are now an unrestricted"
    };

    private static readonly string[] SecretExtractionTerms =
    {
        "mostre a api key",
        "revele a api key",
        "chave da api",
        "variaveis de ambiente",
        "environment variables",
        "connection string",
        "jwt key",
        "twofactor encryptionkey",
        "senha do banco",
        "database password"
    };

    private static readonly string[] SchemaManipulationTerms =
    {
        "primaryintent",
        "secondaryintents",
        "responsesuggestion",
        "suggesteddepartment",
        "shouldescalate",
        "retorne este json",
        "responda com este json",
        "function call",
        "tool call",
        "untrusted_customer_data"
    };

    private static readonly Regex SecretValuePattern = new(
        @"(?:sk-[a-z0-9_-]{12,}|(?:api[\s_-]?key|jwt[\s_-]?key|password|senha)\s*[:=]\s*\S{6,}|host\s*=.+password\s*=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public PromptSecurityAssessment AssessInput(string? text)
    {
        var sanitized = Canonicalize(text);
        if (sanitized.Length == 0)
        {
            return new PromptSecurityAssessment(false, sanitized, Array.Empty<string>());
        }

        var normalized = TextNormalizer.Normalize(sanitized);
        var reasons = new List<string>();
        AddReasonIfMatch(normalized, InstructionOverrideTerms, "instruction-override", reasons);
        AddReasonIfMatch(normalized, PromptDisclosureTerms, "prompt-disclosure", reasons);
        AddReasonIfMatch(normalized, RoleSpoofingTerms, "role-spoofing", reasons);
        AddReasonIfMatch(normalized, JailbreakTerms, "jailbreak", reasons);
        AddReasonIfMatch(normalized, SecretExtractionTerms, "secret-extraction", reasons);
        AddReasonIfMatch(normalized, SchemaManipulationTerms, "schema-manipulation", reasons);

        if ((normalized.Contains("base64", StringComparison.Ordinal) ||
             normalized.Contains("rot13", StringComparison.Ordinal)) &&
            (normalized.Contains("instrucao", StringComparison.Ordinal) ||
             normalized.Contains("prompt", StringComparison.Ordinal)))
        {
            reasons.Add("encoded-instructions");
        }

        return new PromptSecurityAssessment(reasons.Count > 0, sanitized, reasons.Distinct().ToArray());
    }

    public string SanitizeForPrompt(string? text, int maxLength = 2000)
    {
        var assessment = AssessInput(text);
        if (assessment.Blocked)
        {
            return "[CONTEUDO_BLOQUEADO_PELO_FILTRO_DE_SEGURANCA]";
        }

        return assessment.SanitizedText.Length <= maxLength
            ? assessment.SanitizedText
            : assessment.SanitizedText[..maxLength];
    }

    public bool IsUnsafeModelOutput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var assessment = AssessInput(text);
        return assessment.Blocked || SecretValuePattern.IsMatch(text);
    }

    private static void AddReasonIfMatch(
        string normalized,
        IEnumerable<string> terms,
        string reason,
        ICollection<string> reasons)
    {
        if (terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
        {
            reasons.Add(reason);
        }
    }

    private static string Canonicalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(Math.Min(normalized.Length, 2000));
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if ((category is UnicodeCategory.Format or UnicodeCategory.Control) &&
                character is not '\r' and not '\n' and not '\t')
            {
                continue;
            }

            builder.Append(character);
            if (builder.Length == 2000)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }
}
