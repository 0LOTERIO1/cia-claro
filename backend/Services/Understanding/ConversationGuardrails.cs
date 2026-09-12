using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services.Understanding;

public sealed class ConversationGuardrails
{
    private readonly IKnowledgeService _knowledge;
    private readonly AiOptions _options;

    public ConversationGuardrails(IKnowledgeService knowledge, IOptions<AiOptions> options)
    {
        _knowledge = knowledge;
        _options = options.Value;
    }

    public bool TryValidate(ConversationUnderstandingResult? result, out string reason)
    {
        reason = string.Empty;
        if (result is null)
        {
            reason = "empty-result";
            return false;
        }

        if (!Enum.IsDefined(result.PrimaryIntent))
        {
            reason = "invalid-intent";
            return false;
        }

        if (double.IsNaN(result.Confidence) || double.IsInfinity(result.Confidence))
        {
            reason = "invalid-confidence";
            return false;
        }

        return true;
    }

    public ConversationUnderstandingResult Sanitize(ConversationUnderstandingResult result, ConversationUnderstandingRequest request)
    {
        result.Confidence = Math.Clamp(result.Confidence, 0, 1);
        result.SecondaryIntents = (result.SecondaryIntents ?? new List<IntentType>())
            .Where(intent => Enum.IsDefined(intent) && intent != result.PrimaryIntent && intent != IntentType.Unknown)
            .Distinct()
            .Take(4)
            .ToList();

        if (result.SuggestedDepartment.HasValue && !Enum.IsDefined(result.SuggestedDepartment.Value))
        {
            result.SuggestedDepartment = null;
        }

        result.UserMeaning = Truncate(result.UserMeaning, 400);
        result.ResponseSuggestion = Truncate(result.ResponseSuggestion, 1500);
        result.ClarificationQuestion = Truncate(result.ClarificationQuestion, 280);
        result.EscalationReason = Truncate(result.EscalationReason, 240);
        result.SentimentOrUrgency = Truncate(result.SentimentOrUrgency, 40);
        result.ExtractedFacts = SanitizeFacts(result.ExtractedFacts);
        result.KnownFacts = SanitizeFacts(result.KnownFacts);
        result.Inferences = SanitizeFacts(result.Inferences);
        result.Inferences = result.Inferences
            .Where(pair => !result.KnownFacts.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (!_knowledge.AllowsCustomerFacingClaim(result.ResponseSuggestion))
        {
            result.ResponseSuggestion = _knowledge.CommercialFallback;
        }

        if (!_knowledge.AllowsCustomerFacingClaim(result.ClarificationQuestion))
        {
            result.ClarificationQuestion = null;
            result.ShouldAskClarification = false;
        }

        PreventKnownQuestionRepeat(result, request);

        if (result.Confidence < _options.MediumConfidence && result.PrimaryIntent != IntentType.HumanHandoff)
        {
            result.ShouldAskClarification = true;
            result.ClarificationQuestion ??= BuildFallbackClarification(request);
        }

        if (result.ShouldAskClarification && string.IsNullOrWhiteSpace(result.ClarificationQuestion))
        {
            result.ClarificationQuestion = BuildFallbackClarification(request);
        }

        result.ContextUpdates ??= new ContextUpdatesDto();
        result.ContextUpdates.FactsToAppend = (result.ContextUpdates.FactsToAppend ?? new List<string>())
            .Where(fact => !string.IsNullOrWhiteSpace(fact))
            .Select(fact => Truncate(fact, 180)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return result;
    }

    private static void PreventKnownQuestionRepeat(ConversationUnderstandingResult result, ConversationUnderstandingRequest request)
    {
        if (request.ModemRestarted || request.KnownFacts.GetValueOrDefault("modem_restart_attempted") == "true")
        {
            if (LooksLikeRestartQuestion(result.ClarificationQuestion) || LooksLikeRestartQuestion(result.ResponseSuggestion))
            {
                result.ShouldAskClarification = false;
                result.ClarificationQuestion = null;
            }
        }

        if (request.AskedQuestions.Any(asked =>
                !string.IsNullOrWhiteSpace(result.ClarificationQuestion) &&
                asked.Equals(result.ClarificationQuestion, StringComparison.OrdinalIgnoreCase)))
        {
            result.ShouldAskClarification = false;
            result.ClarificationQuestion = null;
        }
    }

    private static bool LooksLikeRestartQuestion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = TextNormalizer.Normalize(text);
        return normalized.Contains("reiniciou o modem", StringComparison.Ordinal)
               || normalized.Contains("reiniciar o modem", StringComparison.Ordinal);
    }

    private static string BuildFallbackClarification(ConversationUnderstandingRequest request)
    {
        var hasInternet = request.IssueType == IssueType.InternetConnection || request.ModemRestarted;
        var hasBilling = request.CurrentRequest?.Contains("cobran", StringComparison.OrdinalIgnoreCase) == true;
        if (hasInternet && hasBilling)
        {
            return "Você está falando da troca do modem ou da cobrança?";
        }

        if (hasInternet)
        {
            return "Quando você diz que não funcionou, o modem continua sem conexão?";
        }

        return "Você está falando de conexão, de um equipamento ou de uma cobrança?";
    }

    private static Dictionary<string, string> SanitizeFacts(Dictionary<string, string>? facts)
    {
        var clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (facts is null)
        {
            return clean;
        }

        foreach (var pair in facts.Take(16))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            clean[Truncate(pair.Key, 60)!] = Truncate(pair.Value, 180)!;
        }

        return clean;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
