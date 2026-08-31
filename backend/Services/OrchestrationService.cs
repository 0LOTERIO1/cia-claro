using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class OrchestrationService : IOrchestrationService
{
    private readonly ITransferRepository _transfers;
    private readonly ISessionRepository _sessions;
    private readonly ILogger<OrchestrationService> _logger;

    public OrchestrationService(
        ITransferRepository transfers,
        ISessionRepository sessions,
        ILogger<OrchestrationService> logger)
    {
        _transfers = transfers;
        _sessions = sessions;
        _logger = logger;
    }

    public Task<RoutingDecision> RouteAsync(
        ConversationSession session,
        IntentType intent,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var target = DecideTarget(session.CurrentDepartment, intent, context);
        return ApplyAsync(session, target, ReasonFor(intent, target), cancellationToken);
    }

    public Task<RoutingDecision> ChangeDepartmentAsync(
        ConversationSession session,
        DepartmentType target,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(session, target, reason, cancellationToken);
    }

    private async Task<RoutingDecision> ApplyAsync(
        ConversationSession session,
        DepartmentType target,
        string reason,
        CancellationToken cancellationToken)
    {
        if (session.CurrentDepartment == target)
        {
            return new RoutingDecision
            {
                Current = session.CurrentDepartment,
                Previous = session.PreviousDepartment,
                Transferred = false
            };
        }

        var from = session.CurrentDepartment;
        session.PreviousDepartment = from;
        session.CurrentDepartment = target;
        session.UpdatedAt = DateTime.UtcNow;

        var transfer = new DepartmentTransfer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            FromDepartment = from,
            ToDepartment = target,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        await _transfers.AddAsync(transfer, cancellationToken);
        session.Transfers.Add(transfer);
        await _sessions.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Department changed. Protocol={Protocol} From={From} To={To} Reason={Reason}",
            session.Protocol, from, target, reason);

        return new RoutingDecision
        {
            Current = target,
            Previous = from,
            Transferred = true,
            Reason = reason,
            Transfer = transfer
        };
    }

    private static DepartmentType DecideTarget(
        DepartmentType current,
        IntentType intent,
        ConversationContext context)
    {
        if (intent == IntentType.HumanHandoff)
        {
            return DepartmentType.HumanAgent;
        }

        if (intent == IntentType.BillingQuestion)
        {
            return DepartmentType.Financial;
        }

        if (intent == IntentType.ModemReplacement)
        {
            return DepartmentType.ModemReplacement;
        }

        if (intent == IntentType.ModemRestarted
            && context.ModemRestarted
            && context.InternetStillDown
            && current is DepartmentType.Triage or DepartmentType.TechnicalSupport)
        {
            return DepartmentType.ModemReplacement;
        }

        if (intent == IntentType.InternetProblem && current == DepartmentType.Triage)
        {
            return DepartmentType.TechnicalSupport;
        }

        return current;
    }

    private static string ReasonFor(IntentType intent, DepartmentType target) => (intent, target) switch
    {
        (IntentType.InternetProblem, DepartmentType.TechnicalSupport) => "Problema de internet identificado na triagem",
        (IntentType.ModemRestarted, DepartmentType.ModemReplacement) => "Modem já reiniciado e problema persistiu",
        (IntentType.ModemReplacement, _) => "Solicitação de troca de modem",
        (IntentType.BillingQuestion, _) => "Dúvida financeira sobre a troca do equipamento",
        (IntentType.HumanHandoff, _) => "Cliente solicitou atendimento humano",
        _ => "Redirecionamento pela orquestração da CIA"
    };
}
