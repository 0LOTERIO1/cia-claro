using System.Text.Json;
using Cia.Api.Entities;

namespace Cia.Api.Services.Understanding;

public sealed class ContextMemoryPayload
{
    public Dictionary<string, string> KnownFacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Inferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> AskedQuestions { get; set; } = new();
    public string? LastAssistantQuestion { get; set; }
    public string? LastPrimaryIntent { get; set; }
    public string? LastResponseSuggestion { get; set; }
    public string? LastCustomerMessage { get; set; }
}

public static class ContextMemory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static ContextMemoryPayload Read(ConversationContext? context)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.AdditionalData))
        {
            return new ContextMemoryPayload();
        }

        var raw = context.AdditionalData.Trim();
        if (!raw.StartsWith('{'))
        {
            return new ContextMemoryPayload { LastCustomerMessage = raw };
        }

        try
        {
            return JsonSerializer.Deserialize<ContextMemoryPayload>(raw, JsonOptions) ?? new ContextMemoryPayload();
        }
        catch (JsonException)
        {
            return new ContextMemoryPayload { LastCustomerMessage = raw };
        }
    }

    public static void Write(ConversationContext context, ContextMemoryPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        context.AdditionalData = json.Length <= 4000 ? json : json[..4000];
    }

    public static void MergeFacts(Dictionary<string, string> target, IReadOnlyDictionary<string, string>? incoming)
    {
        if (incoming is null)
        {
            return;
        }

        foreach (var pair in incoming)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            target[pair.Key.Trim()] = Trim(pair.Value, 180);
        }
    }

    public static void RememberQuestion(ContextMemoryPayload payload, string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        payload.LastAssistantQuestion = Trim(question, 240);
        if (!payload.AskedQuestions.Any(q => q.Equals(payload.LastAssistantQuestion, StringComparison.OrdinalIgnoreCase)))
        {
            payload.AskedQuestions.Add(payload.LastAssistantQuestion);
            if (payload.AskedQuestions.Count > 12)
            {
                payload.AskedQuestions.RemoveAt(0);
            }
        }
    }

    private static string Trim(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
