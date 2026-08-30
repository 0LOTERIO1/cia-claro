using Cia.Api.Configuration;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class AiService : IAiService
{
    private readonly IAiProvider _provider;
    private readonly ILogger<AiService> _logger;

    public AiService(IAiProvider provider, IOptions<AiOptions> options, ILogger<AiService> logger)
    {
        _provider = provider;
        _logger = logger;
        _logger.LogInformation("AI provider selected: {Provider}", options.Value.HasExternalKey ? "External" : "LocalFallback");
    }

    public Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default)
    {
        return _provider.GenerateResponseAsync(message, intent, context, customer, session, cancellationToken);
    }

    public Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        return _provider.GenerateHandoffSummaryAsync(customer, session, context, messages, cancellationToken);
    }
}
