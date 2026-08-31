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
            InternetStillDown = false,
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

        if (intent is IntentType.InternetProblem or IntentType.ModemRestarted or IntentType.ModemReplacement)
        {
            if (context.IssueType != IssueType.InternetConnection)
            {
                context.IssueType = IssueType.InternetConnection;
                changed = true;
            }

            context.OriginalProblem ??= "Internet residencial sem conexão";
        }

        if (intent == IntentType.InternetProblem)
        {
            context.CurrentRequest = "Falha de conexão de internet";
            AppendFact(context, "Problema original: internet sem conexão");
            changed = true;
        }

        if (intent == IntentType.ModemRestarted)
        {
            if (!context.ModemRestarted)
            {
                context.ModemRestarted = true;
                changed = true;
            }

            context.InternetStillDown = true;
            context.TroubleshootingPerformed = "Cliente já reiniciou o modem";
            context.CurrentRequest = "Internet continua sem funcionar após reinício do modem";
            AppendFact(context, "Modem já foi reiniciado");
            AppendFact(context, "Problema persistiu após o procedimento");
            changed = true;
        }

        if (intent == IntentType.ModemReplacement)
        {
            context.CurrentRequest = "Avaliação de substituição do modem";
            AppendFact(context, "Cliente solicitou ou foi encaminhado para troca de modem");
            changed = true;
        }

        if (intent == IntentType.BillingQuestion)
        {
            context.CurrentRequest = "Dúvida sobre cobrança da troca de equipamento";
            AppendFact(context, "Cliente perguntou se a troca do modem gera cobrança");
            changed = true;
        }

        if (changed)
        {
            context.AdditionalData = $"Última atualização a partir da mensagem: {Trim(message)}";
            context.ContextSummary = BuildSummary(context);
            context.UpdatedAt = DateTime.UtcNow;
            await _sessions.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Context updated. SessionId={SessionId} IssueType={IssueType} ModemRestarted={ModemRestarted} InternetStillDown={InternetStillDown}",
                context.SessionId, context.IssueType, context.ModemRestarted, context.InternetStillDown);
        }

        return context;
    }

    private static void AppendFact(ConversationContext context, string fact)
    {
        var current = context.ImportantFacts ?? string.Empty;
        if (current.Contains(fact, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.ImportantFacts = string.IsNullOrWhiteSpace(current) ? fact : $"{current}; {fact}";
    }

    private static string BuildSummary(ConversationContext context)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.OriginalProblem))
        {
            parts.Add(context.OriginalProblem);
        }

        if (context.ModemRestarted)
        {
            parts.Add("modem já reiniciado");
        }

        if (context.InternetStillDown)
        {
            parts.Add("problema persistiu");
        }

        if (!string.IsNullOrWhiteSpace(context.CurrentRequest))
        {
            parts.Add(context.CurrentRequest);
        }

        return string.Join(" | ", parts);
    }

    private static string Trim(string message)
    {
        var value = message.Trim();
        return value.Length <= 180 ? value : value[..180];
    }
}
