using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Prompts;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class ExternalAiProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly LocalFallbackAiProvider _fallback;
    private readonly ILogger<ExternalAiProvider> _logger;

    public ExternalAiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        LocalFallbackAiProvider fallback,
        ILogger<ExternalAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<IntentType> AnalyzeIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await CompleteAsync(
                "Classifique a intenção em exatamente um destes valores: Greeting, InternetProblem, ModemRestarted, ModemReplacement, BillingQuestion, ContinueSupport, HumanHandoff, Unknown. Responda só com o valor.",
                message,
                jsonMode: false,
                cancellationToken);

            if (Enum.TryParse<IntentType>(content, true, out var intent) && intent != IntentType.Unknown)
            {
                return intent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External AI intent analysis failed. Using local fallback.");
        }

        return await _fallback.AnalyzeIntentAsync(message, cancellationToken);
    }

    public async Task<ConversationUnderstandingResult> UnderstandAsync(
        ConversationUnderstandingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = BuildUnderstandingUserPrompt(request);
            var content = await CompleteAsync(AiPrompts.ConversationUnderstandingSystem, user, jsonMode: true, cancellationToken);
            var parsed = ParseUnderstanding(content);
            if (parsed is not null)
            {
                parsed.Provider = nameof(ExternalAiProvider);
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External AI understanding failed. Using local fallback.");
        }

        var fallback = await _fallback.UnderstandAsync(request, cancellationToken);
        fallback.UsedFallback = true;
        return fallback;
    }

    public async Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default,
        IReadOnlyList<Message>? recentMessages = null,
        ConversationUnderstandingResult? understanding = null)
    {
        try
        {
            var history = FormatHistory(recentMessages);
            var prompt =
                $"""
                {AiPrompts.ConversationReplySystem}
                Cliente: {customer.Name} ({customer.Id})
                Protocolo: {session.Protocol}
                Canal atual: {session.CurrentChannel}
                Área atual: {session.CurrentDepartment}
                Área anterior: {session.PreviousDepartment}
                Intenção: {intent}
                Confiança: {understanding?.Confidence}
                Significado: {understanding?.UserMeaning}
                Problema: {context.IssueType}
                Problema original: {context.OriginalProblem}
                Modem reiniciado: {context.ModemRestarted}
                Internet ainda fora: {context.InternetStillDown}
                Pedido atual: {context.CurrentRequest}
                Fatos: {context.ImportantFacts}
                Resumo: {context.ContextSummary}
                Histórico recente:
                {history}
                """;

            var response = await CompleteAsync(prompt, message, jsonMode: false, cancellationToken);
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External AI response generation failed. Using local fallback.");
        }

        return await _fallback.GenerateResponseAsync(
            message, intent, context, customer, session, cancellationToken, recentMessages, understanding);
    }

    public async Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = string.Join("\n", messages.Select(m => $"{m.Sender}: {m.Content}"));
            var prompt =
                $"""
                Gere um resumo estruturado de transbordo humano em português.
                Cliente: {customer.Name}
                Customer ID: {customer.Id}
                Protocolo: {session.Protocol}
                Canal inicial: {session.InitialChannel}
                Canal atual: {session.CurrentChannel}
                Problema: {context.IssueType}
                Modem reiniciado: {context.ModemRestarted}
                Resumo da jornada: {context.ContextSummary}
                Histórico:
                {history}
                """;

            var summary = await CompleteAsync(prompt, "Gere o resumo agora.", jsonMode: false, cancellationToken);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External AI handoff summary failed. Using local fallback.");
        }

        return await _fallback.GenerateHandoffSummaryAsync(customer, session, context, messages, cancellationToken);
    }

    private static string BuildUnderstandingUserPrompt(ConversationUnderstandingRequest request)
    {
        var history = string.Join(
            "\n",
            request.RecentMessages.Select(m => $"{m.Sender}: {m.Content}"));

        var known = string.Join(", ", request.KnownFacts.Select(p => $"{p.Key}={p.Value}"));
        return
            $"""
            Mensagem atual: {request.CurrentMessage}
            Canal: {request.Channel}
            Área atual: {request.CurrentDepartment}
            Status da sessão: {request.SessionStatus}
            Intenção anterior: {request.LastDetectedIntent}
            Resumo: {request.ContextSummary}
            Pedido atual: {request.CurrentRequest}
            Problema original: {request.OriginalProblem}
            Fatos importantes: {request.ImportantFacts}
            Fatos conhecidos: {known}
            Modem reiniciado: {request.ModemRestarted}
            Internet ainda fora: {request.InternetStillDown}
            Última pergunta da CIA: {request.LastAssistantQuestion}
            Histórico recente:
            {history}
            """;
    }

    private static ConversationUnderstandingResult? ParseUnderstanding(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var json = ExtractJson(content);
        ExternalUnderstandingDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ExternalUnderstandingDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (dto is null || !Enum.TryParse<IntentType>(dto.PrimaryIntent, true, out var intent) || !Enum.IsDefined(intent))
        {
            return null;
        }

        var secondary = new List<IntentType>();
        foreach (var item in dto.SecondaryIntents ?? Array.Empty<string>())
        {
            if (Enum.TryParse<IntentType>(item, true, out var parsed) && Enum.IsDefined(parsed) && parsed != intent)
            {
                secondary.Add(parsed);
            }
        }

        DepartmentType? department = null;
        if (!string.IsNullOrWhiteSpace(dto.SuggestedDepartment)
            && Enum.TryParse<DepartmentType>(dto.SuggestedDepartment, true, out var parsedDepartment)
            && Enum.IsDefined(parsedDepartment))
        {
            department = parsedDepartment;
        }

        return new ConversationUnderstandingResult
        {
            PrimaryIntent = intent,
            SecondaryIntents = secondary,
            Confidence = dto.Confidence,
            UserMeaning = dto.UserMeaning,
            ResponseSuggestion = dto.ResponseSuggestion,
            SuggestedDepartment = department,
            ExtractedFacts = dto.ExtractedFacts ?? new Dictionary<string, string>(),
            KnownFacts = dto.KnownFacts ?? new Dictionary<string, string>(),
            Inferences = dto.Inferences ?? new Dictionary<string, string>(),
            ContextUpdates = dto.ContextUpdates ?? new ContextUpdatesDto(),
            MissingInformation = dto.MissingInformation?.ToList() ?? new List<string>(),
            ShouldAskClarification = dto.ShouldAskClarification,
            ClarificationQuestion = dto.ClarificationQuestion,
            ShouldEscalate = dto.ShouldEscalate,
            EscalationReason = dto.EscalationReason,
            TopicChanged = dto.TopicChanged,
            SentimentOrUrgency = dto.SentimentOrUrgency
        };
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }

    private static string FormatHistory(IReadOnlyList<Message>? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return "(sem histórico)";
        }

        return string.Join("\n", messages.TakeLast(16).Select(m => $"{m.Sender}: {m.Content}"));
    }

    private async Task<string> CompleteAsync(string system, string user, bool jsonMode, CancellationToken cancellationToken)
    {
        Exception? last = null;
        var retries = _options.EffectiveMaxRetries;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                object payload = jsonMode
                    ? new
                    {
                        model = _options.Model,
                        messages = new[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        },
                        temperature = 0.2,
                        response_format = new { type = "json_object" }
                    }
                    : new
                    {
                        model = _options.Model,
                        messages = new[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        },
                        temperature = 0.2
                    };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsRetriable(response.StatusCode) && attempt < retries)
                {
                    await Task.Delay(200 * (attempt + 1), cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                return document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                    ?.Trim() ?? string.Empty;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or JsonException)
            {
                last = ex;
                if (attempt < retries && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(200 * (attempt + 1), cancellationToken);
                    continue;
                }

                throw;
            }
        }

        throw last ?? new InvalidOperationException("External AI request failed.");
    }

    private static bool IsRetriable(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    private sealed class ExternalUnderstandingDto
    {
        public string? PrimaryIntent { get; set; }
        public string[]? SecondaryIntents { get; set; }
        public double Confidence { get; set; }
        public string? UserMeaning { get; set; }
        public string? ResponseSuggestion { get; set; }
        public string? SuggestedDepartment { get; set; }
        public Dictionary<string, string>? ExtractedFacts { get; set; }
        public Dictionary<string, string>? KnownFacts { get; set; }
        public Dictionary<string, string>? Inferences { get; set; }
        public ContextUpdatesDto? ContextUpdates { get; set; }
        public string[]? MissingInformation { get; set; }
        public bool ShouldAskClarification { get; set; }
        public string? ClarificationQuestion { get; set; }
        public bool ShouldEscalate { get; set; }
        public string? EscalationReason { get; set; }
        public bool TopicChanged { get; set; }
        public string? SentimentOrUrgency { get; set; }
    }
}
