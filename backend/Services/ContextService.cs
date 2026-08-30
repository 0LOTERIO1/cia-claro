using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class ContextService : IContextService
{
    private readonly IContextRepository _contexts;
    private readonly ISessionRepository _sessions;
    private readonly ILogger<ContextService> _logger;

    public ContextService(
        IContextRepository contexts,
        ISessionRepository sessions,
        ILogger<ContextService> logger)
    {
        _contexts = contexts;
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<ConversationContext> GetOrCreateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var existing = await _contexts.GetBySessionIdAsync(sessionId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = new ConversationContext
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IssueType = IssueType.None,
            ModemRestarted = false,
            UpdatedAt = DateTime.UtcNow
        };

        await _contexts.AddAsync(context, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);
        return context;
    }

    public async Task<ConversationContext> UpdateFromIntentAsync(
        ConversationContext context,
        IntentType intent,
        string message,
        CancellationToken cancellationToken = default)
    {
        var changed = false;

        if (intent is IntentType.InternetProblem or IntentType.ModemRestarted)
        {
            if (context.IssueType != IssueType.InternetConnection)
            {
                context.IssueType = IssueType.InternetConnection;
                changed = true;
            }
        }

        if (intent == IntentType.ModemRestarted && !context.ModemRestarted)
        {
            context.ModemRestarted = true;
            changed = true;
        }

        if (changed)
        {
            context.AdditionalData = $"Última atualização a partir da mensagem: {Trim(message)}";
            context.UpdatedAt = DateTime.UtcNow;
            await _sessions.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Context updated. SessionId={SessionId} IssueType={IssueType} ModemRestarted={ModemRestarted}",
                context.SessionId, context.IssueType, context.ModemRestarted);
        }

        return context;
    }

    private static string Trim(string message)
    {
        var value = message.Trim();
        return value.Length <= 180 ? value : value[..180];
    }
}
