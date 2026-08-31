using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cia.Api.Configuration;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class ExternalAiProvider : IAiProvider
{
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

    public async Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt =
                $"""
                Você é a CIA, assistente de atendimento da Claro.
                Cliente: {customer.Name} ({customer.Id})
                Protocolo: {session.Protocol}
                Canal atual: {session.CurrentChannel}
                Área atual: {session.CurrentDepartment}
                Área anterior: {session.PreviousDepartment}
                Intenção: {intent}
                Problema: {context.IssueType}
                Problema original: {context.OriginalProblem}
                Modem reiniciado: {context.ModemRestarted}
                Internet ainda fora: {context.InternetStillDown}
                Pedido atual: {context.CurrentRequest}
                Resumo: {context.ContextSummary}
                Não pergunte novamente o que já está no contexto. Responda em português, de forma objetiva, sem inventar dados.
                """;

            var response = await CompleteAsync(prompt, message, cancellationToken);
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External AI response generation failed. Using local fallback.");
        }

        return await _fallback.GenerateResponseAsync(message, intent, context, customer, session, cancellationToken);
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
                Histórico:
                {history}
                """;

            var summary = await CompleteAsync(prompt, "Gere o resumo agora.", cancellationToken);
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

    private async Task<string> CompleteAsync(string system, string user, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
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

        using var response = await _httpClient.SendAsync(request, cancellationToken);
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
}
