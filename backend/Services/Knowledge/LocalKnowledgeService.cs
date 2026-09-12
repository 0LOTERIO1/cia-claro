using System.Text.RegularExpressions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services.Knowledge;

public sealed class LocalKnowledgeService : IKnowledgeService
{
    private static readonly Regex CommercialClaim = new(
        @"r\$\s*\d|reais|\bmulta\b|taxa de r|custa r|preco de r|plano custa|valor de r",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
