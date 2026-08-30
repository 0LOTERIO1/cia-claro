using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class IntentService : IIntentService
{
    public IntentType Detect(string message)
    {
        var text = Normalize(message);

        if (ContainsAny(text, "atendente", "humano", "falar com um atendente", "falar com atendente", "transbordo"))
        {
            return IntentType.HumanHandoff;
        }

        if (ContainsAny(text, "continuar meu atendimento", "continuar o atendimento", "continuar atendimento", "quero continuar"))
        {
            return IntentType.ContinueSupport;
        }

        if (ContainsAny(text, "reiniciei", "ja reiniciei", "já reiniciei", "reiniciar o modem", "reiniciei o modem"))
        {
            return IntentType.ModemRestarted;
        }

        if (ContainsAny(text, "internet", "conexao", "conexão", "wifi", "wi-fi", "modem", "sem sinal"))
        {
            return IntentType.InternetProblem;
        }

        if (ContainsAny(text, "ola", "olá", "oi", "bom dia", "boa tarde", "boa noite"))
        {
            return IntentType.Greeting;
        }

        return IntentType.Unknown;
    }

    private static string Normalize(string message)
    {
        return (message ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }
}
