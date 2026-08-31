using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class ConversationService : IConversationService
{
    private readonly ICustomerRepository _customers;
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IContextService _contextService;
    private readonly IIntentService _intentService;
    private readonly IAiService _aiService;
    private readonly IHandoffService _handoffService;
    private readonly IProtocolService _protocolService;
    private readonly IOrchestrationService _orchestration;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        ICustomerRepository customers,
        ISessionRepository sessions,
        IMessageRepository messages,
        IContextService contextService,
        IIntentService intentService,
        IAiService aiService,
        IHandoffService handoffService,
        IProtocolService protocolService,
        IOrchestrationService orchestration,
        ILogger<ConversationService> logger)
    {
        _customers = customers;
        _sessions = sessions;
        _messages = messages;
        _contextService = contextService;
        _intentService = intentService;
        _aiService = aiService;
        _handoffService = handoffService;
        _protocolService = protocolService;
        _orchestration = orchestration;
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

        var intent = _intentService.Detect(request.Content);
        session.DetectedIntent = intent;
        _logger.LogInformation("Intent detected. Protocol={Protocol} Intent={Intent}", session.Protocol, intent);

        context = await _contextService.UpdateFromIntentAsync(context, intent, request.Content, cancellationToken);

        var routing = await _orchestration.RouteAsync(session, intent, context, cancellationToken);

        var contextRestored = routing.Transferred ||
                              (intent == IntentType.ContinueSupport &&
                               (context.IssueType != IssueType.None || context.ModemRestarted));

        if (routing.Transferred)
        {
            _logger.LogInformation(
                "Context transferred. Protocol={Protocol} From={From} To={To} IssueType={IssueType} ModemRestarted={ModemRestarted}",
                session.Protocol, routing.Previous, routing.Current, context.IssueType, context.ModemRestarted);
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
            cancellationToken);

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
        if (intent == IntentType.HumanHandoff)
        {
            handoff = await _handoffService.CreateHandoffAsync(session.Id, cancellationToken);
            session.Status = SessionStatus.Transferred;
        }

        var history = await _messages.GetBySessionIdAsync(session.Id, cancellationToken);
        var transfers = session.Transfers.OrderBy(t => t.CreatedAt).Select(t => t.ToDto()).ToList();
        var transferNotice = routing.Transferred
            ? $"Seu contexto foi transferido para {DepartmentNames.Format(routing.Current)}."
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
            ContextRestored = contextRestored,
            DepartmentChanged = routing.Transferred,
            TransferNotice = transferNotice,
            Context = context.ToDto(),
            AssistantMessage = assistantMessage.ToDto(),
            Handoff = handoff,
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
        var existing = await _sessions.GetActiveByCustomerIdAsync(customer.Id, cancellationToken);
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
        var active = await _sessions.GetActiveByCustomerIdAsync(customer.Id, cancellationToken);
        if (active is not null)
        {
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
