using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Services;

namespace Cia.Api.Tests;

internal sealed class ScriptedAiProvider : IAiProvider
{
    private readonly LocalFallbackAiProvider _fallback = new(new Cia.Api.Services.IntentService());

    public Func<ConversationUnderstandingRequest, ConversationUnderstandingResult>? OnUnderstand { get; set; }
    public Exception? UnderstandException { get; set; }

    public Task<IntentType> AnalyzeIntentAsync(string message, CancellationToken cancellationToken = default)
        => _fallback.AnalyzeIntentAsync(message, cancellationToken);

    public Task<ConversationUnderstandingResult> UnderstandAsync(
        ConversationUnderstandingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (UnderstandException is not null)
        {
            throw UnderstandException;
        }

        if (OnUnderstand is not null)
        {
            return Task.FromResult(OnUnderstand(request));
        }

        return _fallback.UnderstandAsync(request, cancellationToken);
    }

    public Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default,
        IReadOnlyList<Message>? recentMessages = null,
        ConversationUnderstandingResult? understanding = null)
        => _fallback.GenerateResponseAsync(message, intent, context, customer, session, cancellationToken, recentMessages, understanding);

    public Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
        => _fallback.GenerateHandoffSummaryAsync(customer, session, context, messages, cancellationToken);
}
