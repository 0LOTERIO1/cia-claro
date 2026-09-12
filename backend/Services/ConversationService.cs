using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;
using Cia.Api.Services.Understanding;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class ConversationService : IConversationService
{
    private readonly ICustomerRepository _customers;
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IContextService _contextService;
    private readonly IConversationUnderstandingService _understanding;
    private readonly IAiService _aiService;
    private readonly IHandoffService _handoffService;
    private readonly IProtocolService _protocolService;
    private readonly IOrchestrationService _orchestration;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        ICustomerRepository customers,
        ISessionRepository sessions,
        IMessageRepository messages,
        IContextService contextService,
        IConversationUnderstandingService understanding,
        IAiService aiService,
        IHandoffService handoffService,
        IProtocolService protocolService,
        IOrchestrationService orchestration,
        IOptions<AiOptions> aiOptions,
        ILogger<ConversationService> logger)
    {
        _customers = customers;
        _sessions = sessions;
        _messages = messages;
        _contextService = contextService;
        _understanding = understanding;
        _aiService = aiService;
        _handoffService = handoffService;
        _protocolService = protocolService;
        _orchestration = orchestration;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMessage(request);

        var customer = await GetCustomerAsync(request.CustomerId, cancellationToken);
        _logger.LogInformation("Customer identified. CustomerId={CustomerId} Name={Name}", customer.Id, customer.Name);

        var session = await GetOrCreateActiveSessionAsync(customer, request.Channel, cancellationToken);
        var context = await _contextService.GetOrCreateAsync(session.Id, cancellationToken);

        var customerMessage = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sender = MessageSender.Customer,
            Channel = request.Channel,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _messages.AddAsync(customerMessage, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Message received. SessionId={SessionId} Protocol={Protocol} Channel={Channel}",
            session.Id, session.Protocol, request.Channel);

        if (session.Status is SessionStatus.WaitingForAgent or SessionStatus.Transferred)
        {
            session.UpdatedAt = DateTime.UtcNow;
            await _sessions.SaveChangesAsync(cancellationToken);
            return await BuildResponseAsync(session, context, restored: false, transferred: false, handoff: null, cancellationToken);
        }

        var history = await _messages.GetBySessionIdAsync(session.Id, cancellationToken);
        var recent = history
            .OrderByDescending(m => m.CreatedAt)
            .Take(_aiOptions.EffectiveShortTermMessageCount)
            .OrderBy(m => m.CreatedAt)
            .ToList();
        var memory = ContextMemory.Read(context);
        var understanding = await _understanding.UnderstandAsync(
            BuildUnderstandingRequest(request, customer, session, context, recent, memory),
            cancellationToken);

        var intent = understanding.PrimaryIntent;
        session.DetectedIntent = intent;
        _logger.LogInformation(
            "Intent detected. Protocol={Protocol} Intent={Intent} Confidence={Confidence:0.00} Provider={Provider} Fallback={Fallback}",
            session.Protocol, intent, understanding.Confidence, understanding.Provider, understanding.UsedFallback);

        context = await _contextService.ApplyUnderstandingAsync(context, understanding, request.Content, cancellationToken);

        var routingIntent = SelectRoutingIntent(understanding);
        var skipRouting = understanding.ShouldAskClarification
                          && understanding.Confidence < _aiOptions.MediumConfidence
                          && routingIntent is IntentType.Unknown or IntentType.ContinueSupport
                          && !understanding.ShouldEscalate;

        var routing = skipRouting
            ? new RoutingDecision { Current = session.CurrentDepartment, Previous = session.PreviousDepartment }
            : await _orchestration.RouteAsync(session, routingIntent, context, cancellationToken);

        var contextRestored = routing.Transferred ||
                              (intent == IntentType.ContinueSupport &&
                               (context.IssueType != IssueType.None || context.ModemRestarted));

        if (routing.Transferred)
        {
            _logger.LogInformation(
                "Context transferred. Protocol={Protocol} From={From} To={To} IssueType={IssueType} ModemRestarted={ModemRestarted} Orchestration={Intent}",
                session.Protocol, routing.Previous, routing.Current, context.IssueType, context.ModemRestarted, routingIntent);
        }

        if (contextRestored && !routing.Transferred)
        {
            _logger.LogInformation("Context restored. Protocol={Protocol} IssueType={IssueType} ModemRestarted={ModemRestarted}",
                session.Protocol, context.IssueType, context.ModemRestarted);
        }

        var reply = await _aiService.GenerateResponseAsync(
            request.Content,
            intent,
            context,
            customer,
            session,
            cancellationToken,
            recent,
            understanding);

        memory = ContextMemory.Read(context);
        memory.LastResponseSuggestion = reply.Length <= 500 ? reply : reply[..500];
        if (reply.Contains('?', StringComparison.Ordinal))
        {
            ContextMemory.RememberQuestion(memory, reply);
        }

        ContextMemory.Write(context, memory);

        var assistantMessage = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sender = MessageSender.Assistant,
            Channel = request.Channel,
            Content = reply,
            CreatedAt = DateTime.UtcNow
        };

        await _messages.AddAsync(assistantMessage, cancellationToken);

        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        HandoffDto? handoff = null;
        if (intent == IntentType.HumanHandoff || understanding.ShouldEscalate)
        {
            handoff = await _handoffService.CreateHandoffAsync(session.Id, cancellationToken);
        }

        return await BuildResponseAsync(
            session,
            context,
            contextRestored,
            routing.Transferred,
            handoff,
            cancellationToken);
    }

    private static ConversationUnderstandingRequest BuildUnderstandingRequest(
        SendMessageRequest request,
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> recent,
        ContextMemoryPayload memory)
    {
        return new ConversationUnderstandingRequest
        {
            CurrentMessage = request.Content.Trim(),
            RecentMessages = recent.Select(m => new RecentConversationMessage
            {
                Sender = m.Sender,
                Content = m.Content
            }).ToList(),
            Channel = request.Channel,
            CurrentDepartment = session.CurrentDepartment,
            SessionStatus = session.Status,
            LastDetectedIntent = session.DetectedIntent,
            ContextSummary = context.ContextSummary,
            ImportantFacts = context.ImportantFacts,
            CurrentRequest = context.CurrentRequest,
            OriginalProblem = context.OriginalProblem,
            TroubleshootingPerformed = context.TroubleshootingPerformed,
            IssueType = context.IssueType,
            ModemRestarted = context.ModemRestarted,
            InternetStillDown = context.InternetStillDown,
            KnownFacts = memory.KnownFacts,
            Inferences = memory.Inferences,
            AskedQuestions = memory.AskedQuestions,
            LastAssistantQuestion = memory.LastAssistantQuestion
                ?? recent.LastOrDefault(m => m.Sender == MessageSender.Assistant)?.Content,
            CustomerName = customer.Name,
            CustomerId = customer.Id,
            Protocol = session.Protocol
        };
    }

    private static IntentType SelectRoutingIntent(ConversationUnderstandingResult understanding)
    {
        var intents = understanding.AllIntents.ToList();
        if (understanding.ShouldEscalate || intents.Contains(IntentType.HumanHandoff))
        {
            return IntentType.HumanHandoff;
        }

        if (intents.Contains(IntentType.BillingQuestion))
        {
            return IntentType.BillingQuestion;
        }

        if (intents.Contains(IntentType.ModemReplacement))
        {
            return IntentType.ModemReplacement;
        }

        if (intents.Contains(IntentType.ModemRestarted))
        {
            return IntentType.ModemRestarted;
        }

        return understanding.PrimaryIntent;
    }

    private async Task<SendMessageResponse> BuildResponseAsync(
        ConversationSession session,
        ConversationContext context,
        bool restored,
        bool transferred,
        HandoffDto? handoff,
        CancellationToken cancellationToken)
    {
        var history = await _messages.GetBySessionIdAsync(session.Id, cancellationToken);
        var transfers = (session.Transfers ?? Array.Empty<DepartmentTransfer>())
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.ToDto())
            .ToList();
        var transferNotice = session.Status == SessionStatus.WaitingForAgent
            ? "Você entrou na fila de atendimento humano. Um funcionário da Claro assumirá este protocolo em instantes."
            : transferred
                ? $"Seu contexto foi transferido para {DepartmentNames.Format(session.CurrentDepartment)}."
                : null;

        return new SendMessageResponse
        {
            SessionId = session.Id,
            Protocol = session.Protocol,
            Status = session.Status,
            DetectedIntent = session.DetectedIntent,
            CurrentChannel = session.CurrentChannel,
            CurrentDepartment = session.CurrentDepartment,
            PreviousDepartment = session.PreviousDepartment,
            ContextRestored = restored,
            DepartmentChanged = transferred,
            TransferNotice = transferNotice,
            Context = context.ToDto(),
            AssistantMessage = history.LastOrDefault(m => m.Sender == MessageSender.Assistant)?.ToDto(),
            Handoff = handoff,
            HumanAgentRequest = null,
            Messages = history.Select(m => m.ToDto()).ToList(),
            Transfers = transfers
        };
    }

    public async Task<SessionDto> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ValidationAppException("CustomerId é obrigatório.");
        }

        var customer = await GetCustomerAsync(request.CustomerId.Trim(), cancellationToken);
        var existing = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var session = await CreateSessionInternalAsync(customer, request.Channel, cancellationToken);
        await _contextService.GetOrCreateAsync(session.Id, cancellationToken);
        return session.ToDto();
    }

    public async Task<SessionDto> GetSessionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        return session.ToDto();
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await GetCustomerAsync(customerId, cancellationToken);
        var sessions = await _sessions.GetByCustomerIdAsync(customerId, cancellationToken);
        return sessions.Select(s => s.ToDto()).ToList();
    }

    public async Task<SessionDto> ChangeChannelAsync(Guid sessionId, ChannelType channel, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        var previous = session.CurrentChannel;
        session.CurrentChannel = channel;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        var contextRestored = previous != channel && session.Context is not null &&
                              (session.Context.IssueType != IssueType.None || session.Context.ModemRestarted);

        _logger.LogInformation("Channel changed. Protocol={Protocol} From={From} To={To}",
            session.Protocol, previous, channel);

        if (contextRestored)
        {
            _logger.LogInformation("Context restored. Protocol={Protocol} IssueType={IssueType} ModemRestarted={ModemRestarted}",
                session.Protocol, session.Context!.IssueType, session.Context.ModemRestarted);
        }

        return session.ToDto(contextRestored);
    }

    public async Task<SessionDto> ChangeDepartmentAsync(
        Guid sessionId,
        DepartmentType department,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        var routing = await _orchestration.ChangeDepartmentAsync(
            session,
            department,
            string.IsNullOrWhiteSpace(reason) ? "Transferência manual para demonstração" : reason.Trim(),
            cancellationToken);

        return session.ToDto(routing.Transferred, routing.Transferred);
    }

    private async Task<ConversationSession> GetOrCreateActiveSessionAsync(
        Customer customer,
        ChannelType channel,
        CancellationToken cancellationToken)
    {
        var active = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (active is not null)
        {
            if (active.CurrentChannel != channel)
            {
                var previous = active.CurrentChannel;
                active.CurrentChannel = channel;
                active.UpdatedAt = DateTime.UtcNow;
                await _sessions.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Current channel switched. Protocol={Protocol} From={From} To={To} CustomerId={CustomerId}",
                    active.Protocol, previous, channel, customer.Id);
            }

            return active;
        }

        return await CreateSessionInternalAsync(customer, channel, cancellationToken);
    }

    private async Task<ConversationSession> CreateSessionInternalAsync(
        Customer customer,
        ChannelType channel,
        CancellationToken cancellationToken)
    {
        var protocol = await _protocolService.GenerateAsync(cancellationToken);
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            Protocol = protocol,
            CustomerId = customer.Id,
            Customer = customer,
            InitialChannel = channel,
            CurrentChannel = channel,
            CurrentDepartment = DepartmentType.Triage,
            Status = SessionStatus.Active,
            DetectedIntent = IntentType.Unknown,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _sessions.AddAsync(session, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Session created. Protocol={Protocol} CustomerId={CustomerId} Channel={Channel}",
            protocol, customer.Id, channel);

        return session;
    }

    private async Task<Customer> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        return await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException($"Cliente {customerId} não encontrado.");
    }

    private static void ValidateMessage(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ValidationAppException("CustomerId é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationAppException("A mensagem não pode ser vazia.");
        }

        if (request.Content.Trim().Length > 2000)
        {
            throw new ValidationAppException("A mensagem excede o tamanho máximo de 2000 caracteres.");
        }
    }
}
