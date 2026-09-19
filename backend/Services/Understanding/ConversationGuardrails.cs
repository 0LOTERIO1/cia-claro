using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services.Understanding;

public sealed class ConversationGuardrails
{
    private static readonly HashSet<string> AllowedFactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "modem_restart_attempted",
        "restart_count",
        "modem_light",
        "issue_persists",
        "declined_replacement",
        "wants_human_agent",
        "possible_equipment_failure"
    };

    private readonly IKnowledgeService _knowledge;
    private readonly AiOptions _options;
    private readonly PromptSecurityService _promptSecurity;

    public ConversationGuardrails(
        IKnowledgeService knowledge,
        IOptions<AiOptions> options,
        PromptSecurityService promptSecurity)
    {
        _knowledge = knowledge;
        _options = options.Value;
        _promptSecurity = promptSecurity;
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

        if (result.ContextUpdates?.IssueType is { } issueType && !Enum.IsDefined(issueType))
        {
            reason = "invalid-context-issue";
            return false;
        }

        var generatedTexts = new[]
        {
            result.UserMeaning,
            result.ResponseSuggestion,
            result.ClarificationQuestion,
            result.EscalationReason,
            result.ContextUpdates?.CurrentRequest,
            result.ContextUpdates?.OriginalProblem,
            result.ContextUpdates?.TroubleshootingPerformed
        };
        if (generatedTexts.Any(_promptSecurity.IsUnsafeModelOutput) ||
            ContainsUnsafeFact(result.ExtractedFacts) ||
            ContainsUnsafeFact(result.KnownFacts) ||
            ContainsUnsafeFact(result.Inferences))
        {
            reason = "unsafe-model-output";
            return false;
        }

        return true;
    }

    public ConversationUnderstandingResult Sanitize(ConversationUnderstandingResult result, ConversationUnderstandingRequest request)
    {
        result.Confidence = Math.Clamp(result.Confidence, 0, 1);
        var explicitHumanRequest = HasExplicitHumanHandoffRequest(request);
        if (!explicitHumanRequest)
        {
            if (result.PrimaryIntent == IntentType.HumanHandoff)
            {
                result.PrimaryIntent = IntentType.Unknown;
                result.Confidence = Math.Min(result.Confidence, 0.4);
            }

            result.ShouldEscalate = false;
            result.EscalationReason = null;
            if (result.SuggestedDepartment == DepartmentType.HumanAgent)
            {
                result.SuggestedDepartment = null;
            }
        }

        result.SecondaryIntents = (result.SecondaryIntents ?? new List<IntentType>())
            .Where(intent =>
                Enum.IsDefined(intent) &&
                intent != result.PrimaryIntent &&
                intent != IntentType.Unknown &&
                (intent != IntentType.HumanHandoff || explicitHumanRequest))
            .Distinct()
            .Take(4)
            .ToList();

        if (result.SuggestedDepartment.HasValue && !Enum.IsDefined(result.SuggestedDepartment.Value))
        {
            result.SuggestedDepartment = null;
        }

        result.UserMeaning = TruncateSafe(result.UserMeaning, 400);
        result.ResponseSuggestion = TruncateSafe(result.ResponseSuggestion, 1500);
        result.ClarificationQuestion = TruncateSafe(result.ClarificationQuestion, 280);
        result.EscalationReason = TruncateSafe(result.EscalationReason, 240);
        result.SentimentOrUrgency = SanitizeSentiment(result.SentimentOrUrgency);
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
        result.ContextUpdates.CurrentRequest = TruncateSafe(result.ContextUpdates.CurrentRequest, 500);
        result.ContextUpdates.OriginalProblem = TruncateSafe(result.ContextUpdates.OriginalProblem, 500);
        result.ContextUpdates.TroubleshootingPerformed =
            TruncateSafe(result.ContextUpdates.TroubleshootingPerformed, 1000);
        result.ContextUpdates.FactsToAppend = (result.ContextUpdates.FactsToAppend ?? new List<string>())
            .Where(fact => !string.IsNullOrWhiteSpace(fact))
            .Where(fact => !_promptSecurity.IsUnsafeModelOutput(fact))
            .Select(fact => Truncate(fact, 180)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        result.MissingInformation = (result.MissingInformation ?? new List<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Where(item => !_promptSecurity.IsUnsafeModelOutput(item))
            .Select(item => Truncate(item, 80)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return result;
    }

    private static bool HasExplicitHumanHandoffRequest(ConversationUnderstandingRequest request)
    {
        var message = TextNormalizer.Normalize(request.CurrentMessage);
        var explicitlyRequestsHuman = TextNormalizer.ContainsAny(
            message,
            "atendente",
            "atendimento humano",
            "falar com uma pessoa",
            "falar com alguem",
            "falar com supervisor",
            "falar com operador",
            "me transfira",
            "transferir para");
        if (explicitlyRequestsHuman)
        {
            return true;
        }

        var lastQuestion = TextNormalizer.Normalize(request.LastAssistantQuestion);
        var confirmsPreviousQuestion = TextNormalizer.EqualsAny(
            message,
            "sim",
            "quero",
            "pode",
            "pode ser",
            "por favor");
        return confirmsPreviousQuestion &&
               TextNormalizer.ContainsAny(lastQuestion, "atendente", "humano", "pessoa", "transferir");
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

    private Dictionary<string, string> SanitizeFacts(Dictionary<string, string>? facts)
    {
        var clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (facts is null)
        {
            return clean;
        }

        foreach (var pair in facts.Take(16))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value) ||
                !AllowedFactKeys.Contains(pair.Key.Trim()) ||
                _promptSecurity.IsUnsafeModelOutput(pair.Value))
            {
                continue;
            }

            clean[Truncate(pair.Key, 60)!] = Truncate(pair.Value, 180)!;
        }

        return clean;
    }

    private bool ContainsUnsafeFact(Dictionary<string, string>? facts)
    {
        return facts?.Any(pair =>
            _promptSecurity.IsUnsafeModelOutput(pair.Key) ||
            _promptSecurity.IsUnsafeModelOutput(pair.Value)) == true;
    }

    private string? TruncateSafe(string? value, int max)
    {
        return _promptSecurity.IsUnsafeModelOutput(value) ? null : Truncate(value, max);
    }

    private static string? SanitizeSentiment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "neutral" or "frustrated" or "urgent" ? normalized : null;
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
