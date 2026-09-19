using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public sealed class RecentConversationMessage
{
    public MessageSender Sender { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed class ConversationUnderstandingRequest
{
    public string CurrentMessage { get; init; } = string.Empty;
    public IReadOnlyList<RecentConversationMessage> RecentMessages { get; init; } = Array.Empty<RecentConversationMessage>();
    public ChannelType Channel { get; init; }
    public DepartmentType CurrentDepartment { get; init; }
    public SessionStatus SessionStatus { get; init; }
    public IntentType? LastDetectedIntent { get; init; }
    public string? ContextSummary { get; init; }
    public string? ImportantFacts { get; init; }
    public string? CurrentRequest { get; init; }
    public string? OriginalProblem { get; init; }
    public string? TroubleshootingPerformed { get; init; }
    public IssueType IssueType { get; init; }
    public bool ModemRestarted { get; init; }
    public bool InternetStillDown { get; init; }
    public IReadOnlyDictionary<string, string> KnownFacts { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Inferences { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> AskedQuestions { get; init; } = Array.Empty<string>();
    public string? LastAssistantQuestion { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
}

public sealed class ContextUpdatesDto
{
    public IssueType? IssueType { get; set; }
    public bool? ModemRestarted { get; set; }
    public bool? InternetStillDown { get; set; }
    public string? OriginalProblem { get; set; }
    public string? TroubleshootingPerformed { get; set; }
    public string? CurrentRequest { get; set; }
    public List<string> FactsToAppend { get; set; } = new();
}

public sealed class ConversationUnderstandingResult
{
    public IntentType PrimaryIntent { get; set; } = IntentType.Unknown;
    public List<IntentType> SecondaryIntents { get; set; } = new();
    public double Confidence { get; set; }
    public string? UserMeaning { get; set; }
    public string? ResponseSuggestion { get; set; }
    public DepartmentType? SuggestedDepartment { get; set; }
    public Dictionary<string, string> ExtractedFacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> KnownFacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Inferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ContextUpdatesDto ContextUpdates { get; set; } = new();
    public List<string> MissingInformation { get; set; } = new();
    public bool ShouldAskClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
    public bool ShouldEscalate { get; set; }
    public string? EscalationReason { get; set; }
    public bool TopicChanged { get; set; }
    public string? SentimentOrUrgency { get; set; }
    public string Provider { get; set; } = "LocalFallback";
    public bool UsedFallback { get; set; }
    public long LatencyMs { get; set; }
    public bool SecurityBlocked { get; set; }
    public IReadOnlyList<string> SecurityReasons { get; set; } = Array.Empty<string>();

    public IEnumerable<IntentType> AllIntents
    {
        get
        {
            yield return PrimaryIntent;
            foreach (var intent in SecondaryIntents)
            {
                yield return intent;
            }
        }
    }
}
