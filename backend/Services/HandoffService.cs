using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class HandoffService : IHandoffService
{
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IContextService _contextService;
    private readonly IHandoffRepository _handoffs;
    private readonly IHumanAgentRequestRepository _humanRequests;
    private readonly IAiService _aiService;
    private readonly ILogger<HandoffService> _logger;

    public HandoffService(
        ISessionRepository sessions,
        IMessageRepository messages,
        IContextService contextService,
        IHandoffRepository handoffs,
        IHumanAgentRequestRepository humanRequests,
        IAiService aiService,
        ILogger<HandoffService> logger)
    {
        _sessions = sessions;
        _messages = messages;
        _contextService = contextService;
        _handoffs = handoffs;
        _humanRequests = humanRequests;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<HandoffDto> CreateHandoffAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        var openRequest = await _humanRequests.GetOpenBySessionIdAsync(sessionId, cancellationToken);
        if (openRequest is not null)
        {
            var existing = await _handoffs.GetLatestBySessionIdAsync(sessionId, cancellationToken);
            if (existing is not null)
            {
                return existing.ToDto();
            }
        }

        var context = await _contextService.GetOrCreateAsync(session.Id, cancellationToken);
        var messages = await _messages.GetBySessionIdAsync(session.Id, cancellationToken);
        var summary = await _aiService.GenerateHandoffSummaryAsync(
            session.Customer,
            session,
            context,
            messages,
            cancellationToken);

        var handoff = new Handoff
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Summary = summary,
            Status = HandoffStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _handoffs.AddAsync(handoff, cancellationToken);

        if (openRequest is null)
        {
            await _humanRequests.AddAsync(new HumanAgentRequest
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Status = HumanAgentRequestStatus.Waiting,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        session.Status = SessionStatus.WaitingForAgent;
        session.DetectedIntent = IntentType.HumanHandoff;
        if (session.CurrentDepartment != DepartmentType.HumanAgent)
        {
            session.PreviousDepartment = session.CurrentDepartment;
            session.CurrentDepartment = DepartmentType.HumanAgent;
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Human agent queue created. Protocol={Protocol} Status={Status}",
            session.Protocol, session.Status);

        return handoff.ToDto();
    }
}
