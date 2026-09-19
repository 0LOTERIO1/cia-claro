using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;
using Cia.Api.Configuration;
using Cia.Api.Services.Understanding;

namespace Cia.Api.Services;

public class AiService : IAiService
{
    private readonly IAiProvider _provider;
    private readonly LocalFallbackAiProvider _fallback;
    private readonly IKnowledgeService _knowledge;
    private readonly PromptSecurityService _promptSecurity;
    private readonly SensitiveDataRedactor _sensitiveData;
    private readonly ILogger<AiService> _logger;

    public AiService(
        IAiProvider provider,
        LocalFallbackAiProvider fallback,
        IKnowledgeService knowledge,
        PromptSecurityService promptSecurity,
        SensitiveDataRedactor sensitiveData,
        IOptions<AiOptions> options,
        ILogger<AiService> logger)
    {
        _provider = provider;
        _fallback = fallback;
        _knowledge = knowledge;
        _promptSecurity = promptSecurity;
        _sensitiveData = sensitiveData;
        _logger = logger;
        _logger.LogInformation("AI provider selected: {Provider}", options.Value.HasExternalKey ? "External" : "LocalFallback");
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
        if (understanding?.SecurityBlocked == true || _promptSecurity.AssessInput(message).Blocked)
        {
            return PromptSecurityService.SafeRefusal;
        }

        if (intent == IntentType.Unknown)
        {
            return await _fallback.GenerateResponseAsync(
                message, intent, context, customer, session, cancellationToken, recentMessages, understanding);
        }

        var response = await _provider.GenerateResponseAsync(
            message, intent, context, customer, session, cancellationToken, recentMessages, understanding);
        if (IsSafeCustomerResponse(response))
        {
            return response.Trim();
        }

        _logger.LogWarning("Unsafe AI response blocked. Provider={Provider}", _provider.GetType().Name);
        var fallback = await _fallback.GenerateResponseAsync(
            message, intent, context, customer, session, cancellationToken, recentMessages, understanding);
        return IsSafeCustomerResponse(fallback)
            ? fallback.Trim()
            : "Não consegui gerar uma resposta segura. Posso encaminhar você para um atendente humano.";
    }

    public async Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        var summary = await _provider.GenerateHandoffSummaryAsync(
            customer, session, context, messages, cancellationToken);
        if (IsSafeInternalSummary(summary))
        {
            return summary.Trim();
        }

        _logger.LogWarning("Unsafe AI handoff summary blocked. Provider={Provider}", _provider.GetType().Name);
        return await _fallback.GenerateHandoffSummaryAsync(
            customer, session, context, messages, cancellationToken);
    }

    private bool IsSafeCustomerResponse(string? response)
    {
        return !string.IsNullOrWhiteSpace(response)
            && response.Length <= 2000
            && !_promptSecurity.IsUnsafeModelOutput(response)
            && !_sensitiveData.ContainsSensitiveData(response)
            && _knowledge.AllowsCustomerFacingClaim(response);
    }

    private bool IsSafeInternalSummary(string? summary)
    {
        return !string.IsNullOrWhiteSpace(summary)
            && summary.Length <= 4000
            && !_promptSecurity.IsUnsafeModelOutput(summary)
            && !_sensitiveData.ContainsSensitiveData(summary);
    }
}
