using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Services.Understanding;

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

    public Task<ConversationContext> UpdateFromIntentAsync(
        ConversationContext context,
        IntentType intent,
        string message,
        CancellationToken cancellationToken = default)
    {
        var understanding = new ConversationUnderstandingResult
        {
            PrimaryIntent = intent,
            Confidence = 1,
            ContextUpdates = UpdatesFromIntent(intent)
        };

        if (intent == IntentType.ModemRestarted)
        {
            understanding.KnownFacts["modem_restart_attempted"] = "true";
            understanding.KnownFacts["issue_persists"] = "true";
        }

        return ApplyUnderstandingAsync(context, understanding, message, cancellationToken);
    }

    private static ContextUpdatesDto UpdatesFromIntent(IntentType intent)
    {
        var updates = new ContextUpdatesDto();
        if (intent is IntentType.InternetProblem or IntentType.ModemRestarted or IntentType.ModemReplacement)
        {
            updates.IssueType = IssueType.InternetConnection;
            updates.OriginalProblem = "Internet residencial sem conexão";
        }

        if (intent == IntentType.InternetProblem)
        {
            updates.CurrentRequest = "Falha de conexão de internet";
            updates.FactsToAppend.Add("Problema original: internet sem conexão");
        }

        if (intent == IntentType.ModemRestarted)
        {
            updates.ModemRestarted = true;
            updates.InternetStillDown = true;
            updates.TroubleshootingPerformed = "Cliente já reiniciou o modem";
            updates.CurrentRequest = "Internet continua sem funcionar após reinício do modem";
            updates.FactsToAppend.Add("Modem já foi reiniciado");
            updates.FactsToAppend.Add("Problema persistiu após o procedimento");
        }

        if (intent == IntentType.ModemReplacement)
        {
            updates.CurrentRequest = "Avaliação de substituição do modem";
            updates.FactsToAppend.Add("Cliente solicitou ou foi encaminhado para troca de modem");
        }

        if (intent == IntentType.BillingQuestion)
        {
            updates.CurrentRequest = "Dúvida sobre cobrança da troca de equipamento";
            updates.FactsToAppend.Add("Cliente perguntou se a troca do modem gera cobrança");
        }

        return updates;
    }

    public async Task<ConversationContext> ApplyUnderstandingAsync(
        ConversationContext context,
        ConversationUnderstandingResult understanding,
        string message,
        CancellationToken cancellationToken = default)
    {
        var memory = ContextMemory.Read(context);
        var changed = false;
        var updates = understanding.ContextUpdates ?? new ContextUpdatesDto();

        if (updates.IssueType is IssueType issue && issue != IssueType.None && context.IssueType != issue)
        {
            context.IssueType = issue;
            changed = true;
        }

        if (updates.ModemRestarted == true && !context.ModemRestarted)
        {
            context.ModemRestarted = true;
            changed = true;
        }

        if (updates.InternetStillDown == true && !context.InternetStillDown)
        {
            context.InternetStillDown = true;
            changed = true;
        }

        if (context.OriginalProblem is null && !string.IsNullOrWhiteSpace(updates.OriginalProblem))
        {
            context.OriginalProblem = Truncate(updates.OriginalProblem, 240);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(updates.TroubleshootingPerformed)
            && context.TroubleshootingPerformed != updates.TroubleshootingPerformed)
        {
            context.TroubleshootingPerformed = Truncate(updates.TroubleshootingPerformed, 240);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(updates.CurrentRequest)
            && !string.Equals(context.CurrentRequest, updates.CurrentRequest, StringComparison.Ordinal))
        {
            context.CurrentRequest = Truncate(updates.CurrentRequest, 240);
            changed = true;
        }

        foreach (var fact in updates.FactsToAppend)
        {
            if (AppendFact(context, fact))
            {
                changed = true;
            }
        }

        ContextMemory.MergeFacts(memory.KnownFacts, understanding.KnownFacts);
        ContextMemory.MergeFacts(memory.Inferences, understanding.Inferences);
        foreach (var inferenceKey in memory.KnownFacts.Keys)
        {
            memory.Inferences.Remove(inferenceKey);
        }

        memory.LastPrimaryIntent = understanding.PrimaryIntent.ToString();
        memory.LastCustomerMessage = Truncate(message, 180);
        memory.LastResponseSuggestion = Truncate(understanding.ResponseSuggestion ?? string.Empty, 500);

        var summary = BuildSummary(context);
        if (!string.Equals(context.ContextSummary, summary, StringComparison.Ordinal))
        {
            context.ContextSummary = summary;
            changed = true;
        }

        ContextMemory.Write(context, memory);
        context.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        if (changed)
        {
            _logger.LogInformation(
                "Context updated. SessionId={SessionId} IssueType={IssueType} ModemRestarted={ModemRestarted} InternetStillDown={InternetStillDown} TopicChanged={TopicChanged}",
                context.SessionId, context.IssueType, context.ModemRestarted, context.InternetStillDown, understanding.TopicChanged);
        }

        return context;
    }

    private static bool AppendFact(ConversationContext context, string fact)
    {
        if (string.IsNullOrWhiteSpace(fact))
        {
            return false;
        }

        var current = context.ImportantFacts ?? string.Empty;
        if (current.Contains(fact, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        context.ImportantFacts = string.IsNullOrWhiteSpace(current) ? fact : $"{current}; {fact}";
        return true;
    }

    private static string BuildSummary(ConversationContext context)
    {
        var parts = new List<string>();
        if (context.IssueType == IssueType.InternetConnection || !string.IsNullOrWhiteSpace(context.OriginalProblem))
        {
            parts.Add(context.OriginalProblem ?? "Cliente iniciou atendimento por falta de internet.");
            if (context.OriginalProblem is not null && !context.OriginalProblem.Contains("iniciou atendimento", StringComparison.OrdinalIgnoreCase))
            {
                parts[0] = "Cliente iniciou atendimento por falta de internet.";
            }
        }

        if (context.ModemRestarted)
        {
            parts.Add("Informou que já reiniciou o modem.");
        }

        if (context.InternetStillDown)
        {
            parts.Add("Problema persiste.");
        }

        if (context.CurrentRequest?.Contains("substit", StringComparison.OrdinalIgnoreCase) == true
            || context.CurrentRequest?.Contains("troca", StringComparison.OrdinalIgnoreCase) == true
            || context.ImportantFacts?.Contains("troca de modem", StringComparison.OrdinalIgnoreCase) == true)
        {
            parts.Add("Foi direcionado para avaliação de troca.");
        }

        if (context.CurrentRequest?.Contains("cobran", StringComparison.OrdinalIgnoreCase) == true
            || context.ImportantFacts?.Contains("cobrança", StringComparison.OrdinalIgnoreCase) == true)
        {
            parts.Add("Posteriormente perguntou sobre cobrança.");
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(context.CurrentRequest))
        {
            parts.Add(context.CurrentRequest);
        }

        return string.Join(" ", parts);
    }

    private static string Truncate(string message, int max)
    {
        var value = message.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
