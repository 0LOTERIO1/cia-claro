using System.Diagnostics;
using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Cia.Api.Services.Understanding;

namespace Cia.Api.Services;

public sealed class ConversationUnderstandingService : IConversationUnderstandingService
{
    private readonly IAiProvider _provider;
    private readonly LocalFallbackAiProvider _fallback;
    private readonly ConversationGuardrails _guardrails;
    private readonly ILogger<ConversationUnderstandingService> _logger;

    public ConversationUnderstandingService(
        IAiProvider provider,
        LocalFallbackAiProvider fallback,
        ConversationGuardrails guardrails,
        ILogger<ConversationUnderstandingService> logger)
    {
        _provider = provider;
        _fallback = fallback;
        _guardrails = guardrails;
        _logger = logger;
    }

    public async Task<ConversationUnderstandingResult> UnderstandAsync(
        ConversationUnderstandingRequest request,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var providerName = _provider.GetType().Name;
        var usedFallback = false;
        ConversationUnderstandingResult result;

        try
        {
            result = await _provider.UnderstandAsync(request, cancellationToken);
            if (!_guardrails.TryValidate(result, out var reason))
            {
                usedFallback = true;
                providerName = nameof(LocalFallbackAiProvider);
                _logger.LogWarning("Conversation understanding invalid. Reason={Reason} Fallback=true", reason);
                result = await _fallback.UnderstandAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            usedFallback = true;
            providerName = nameof(LocalFallbackAiProvider);
            _logger.LogWarning(ex, "Conversation understanding provider failed. Fallback=true");
            result = await _fallback.UnderstandAsync(request, cancellationToken);
        }

        result = _guardrails.Sanitize(result, request);
        watch.Stop();
        result.Provider = providerName;
        result.UsedFallback = usedFallback;
        result.LatencyMs = watch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Conversation understanding. Provider={Provider} Intent={Intent} Confidence={Confidence:0.00} Clarification={Clarification} Fallback={Fallback} LatencyMs={LatencyMs} DepartmentHint={Department}",
            result.Provider,
            result.PrimaryIntent,
            result.Confidence,
            result.ShouldAskClarification,
            result.UsedFallback,
            result.LatencyMs,
            result.SuggestedDepartment);

        return result;
    }
}
