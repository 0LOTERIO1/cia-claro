using System.Text.RegularExpressions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services.Knowledge;

public sealed class LocalKnowledgeService : IKnowledgeService
{
    private static readonly Regex CommercialClaim = new(
        @"r\$\s*\d|reais|\bmulta\b|taxa de r|custa r|pre[cç]o de r|plano custa|valor de r|" +
        @"\bsem custo\b|\bgratuit[oa]\b|\bgr[aá]tis\b|\bem at[eé] \d+|\bprazo (?:de )?\d+|" +
        @"\b\d+\s*(?:horas?|dias?|mbps|mega(?:s)?|gigas?|gb)\b|" +
        @"cobertura (?:est[aá] )?confirmada|dispon[ií]vel na sua regi[aã]o|\bgarantid[oa]\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public string CommercialFallback =>
        "Essa informação depende das condições da sua conta. Posso encaminhar para o setor responsável.";

    public bool AllowsCustomerFacingClaim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return !CommercialClaim.IsMatch(text);
    }
}
