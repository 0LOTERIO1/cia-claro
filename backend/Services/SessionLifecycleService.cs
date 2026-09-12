using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class SessionLifecycleService : ISessionLifecycleService
{
    public const string OfferRestartMessage =
        "Você já possui um atendimento em andamento.\nEnvie /continuar para continuar ou /novo para iniciar outro atendimento.";

    public const string ContinuedMessage = "Certo. Pode continuar por aqui.";
    public const string EndedMessage = "Atendimento encerrado. Quando precisar, envie /start para começar novamente.";
    public const string NoSessionMessage = "Não há atendimento em andamento. Envie /start para começar.";
    public const string HumanSessionMessage = "Você já possui um atendimento com nossa equipe em andamento.";
    public const string HumanEndMessage =
        "Seu atendimento já está com nossa equipe. Para encerrar, aguarde o atendente ou solicite o encerramento por aqui.";

    private readonly ICustomerRepository _customers;
    private readonly ISessionRepository _sessions;
    private readonly IConversationService _conversations;
    private readonly IMessageRepository _messages;
    private readonly ILogger<SessionLifecycleService> _logger;

    public SessionLifecycleService(
        ICustomerRepository customers,
        ISessionRepository sessions,
        IConversationService conversations,
        IMessageRepository messages,
        ILogger<SessionLifecycleService> logger)
    {
        _customers = customers;
        _sessions = sessions;
        _conversations = conversations;
        _messages = messages;
        _logger = logger;
    }

    public async Task<SessionLifecycleResult> HandleStartAsync(
        string customerId,
        ChannelType channel,
        string? firstName = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var open = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);

        if (open is not null && SessionRules.IsHumanSession(open))
        {
            Log(customer.Id, "start", open, null, TelegramCommandActions.HumanSessionPreserved);
            return Result(TelegramCommandActions.HumanSessionPreserved, HumanSessionMessage, open);
        }

        if (open is not null && SessionRules.IsAiActive(open))
        {
            await TouchChannelAsync(open, channel, cancellationToken);
            Log(customer.Id, "start", open, null, "OfferedContinueOrRestart");
            return Result("OfferedContinueOrRestart", OfferRestartMessage, open);
        }

        var created = await CreateFreshSessionAsync(customer, channel, firstName, cancellationToken);
        Log(customer.Id, "start", null, created.Id, TelegramCommandActions.CreatedNewSession);
        return new SessionLifecycleResult
        {
            Action = TelegramCommandActions.CreatedNewSession,
            Message = created.Greeting,
            NewSessionId = created.Id,
            Session = created.Session
        };
    }

    public async Task<SessionLifecycleResult> ContinueAsync(
        string customerId,
        ChannelType channel,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var open = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (open is null)
        {
            Log(customer.Id, "continuar", null, null, "NoOpenSession");
            return Result("NoOpenSession", NoSessionMessage);
        }

        if (SessionRules.IsHumanSession(open))
        {
            Log(customer.Id, "continuar", open, null, TelegramCommandActions.HumanSessionPreserved);
            return Result(TelegramCommandActions.HumanSessionPreserved, HumanSessionMessage, open);
        }

        await TouchChannelAsync(open, channel, cancellationToken);
        Log(customer.Id, "continuar", open, null, TelegramCommandActions.ContinuedActiveSession);
        return Result(TelegramCommandActions.ContinuedActiveSession, ContinuedMessage, open);
    }

    public async Task<SessionLifecycleResult> RestartAsync(
        string customerId,
        ChannelType channel,
        string? firstName = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var open = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (open is not null && SessionRules.IsHumanSession(open))
        {
            Log(customer.Id, "novo", open, null, TelegramCommandActions.HumanSessionPreserved);
            return Result(TelegramCommandActions.HumanSessionPreserved, HumanSessionMessage, open);
        }

        Guid? closedId = null;
        SessionStatus? oldStatus = open?.Status;
        if (open is not null && SessionRules.IsAiActive(open))
        {
            closedId = open.Id;
            SessionRules.Close(open, SessionClosureReason.CustomerRestarted);
            await _sessions.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Session closed by customer restart. CustomerId={CustomerId} SessionId={SessionId} OldStatus={OldStatus} ClosureReason={ClosureReason}",
                customer.Id, open.Id, oldStatus, SessionClosureReason.CustomerRestarted);
        }

        var created = await CreateFreshSessionAsync(customer, channel, firstName, cancellationToken);
        Log(customer.Id, "novo", closedId is null ? null : open, created.Id, TelegramCommandActions.CreatedNewSession);
        return new SessionLifecycleResult
        {
            Action = TelegramCommandActions.CreatedNewSession,
            Message = created.Greeting,
            ClosedSessionId = closedId,
            NewSessionId = created.Id,
            Session = created.Session
        };
    }

    public async Task<SessionLifecycleResult> EndAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var open = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (open is null)
        {
            Log(customer.Id, "encerrar", null, null, "NoOpenSession");
            return Result("NoOpenSession", NoSessionMessage);
        }

        if (SessionRules.IsHumanSession(open))
        {
            Log(customer.Id, "encerrar", open, null, TelegramCommandActions.HumanSessionPreserved);
            return Result(TelegramCommandActions.HumanSessionPreserved, HumanEndMessage, open);
        }

        var oldStatus = open.Status;
        SessionRules.Close(open, SessionClosureReason.CustomerEnded);
        await _sessions.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Session ended by customer. CustomerId={CustomerId} SessionId={SessionId} OldStatus={OldStatus} ClosureReason={ClosureReason}",
            customer.Id, open.Id, oldStatus, SessionClosureReason.CustomerEnded);

        return new SessionLifecycleResult
        {
            Action = "EndedSession",
            Message = EndedMessage,
            ClosedSessionId = open.Id,
            Session = open.ToDto()
        };
    }

    private async Task<(Guid Id, string Greeting, SessionDto Session)> CreateFreshSessionAsync(
        Customer customer,
        ChannelType channel,
        string? firstName,
        CancellationToken cancellationToken)
    {
        var created = await _conversations.CreateSessionAsync(
            new CreateSessionRequest { CustomerId = customer.Id, Channel = channel },
            cancellationToken);

        var greeting = TelegramCommandHandler.BuildGreeting(firstName, customer.Name);
        await _messages.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            SessionId = created.Id,
            Sender = MessageSender.Assistant,
            Channel = channel,
            Content = greeting,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);

        return (created.Id, greeting, created);
    }

    private async Task TouchChannelAsync(ConversationSession session, ChannelType channel, CancellationToken cancellationToken)
    {
        if (session.CurrentChannel == channel)
        {
            return;
        }

        session.CurrentChannel = channel;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        return await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new Exceptions.NotFoundException("Cliente não encontrado.");
    }

    private void Log(string customerId, string command, ConversationSession? current, Guid? newSessionId, string action)
    {
        _logger.LogInformation(
            "Session lifecycle command. CustomerId={CustomerId} Command={Command} SessionId={SessionId} OldStatus={OldStatus} ClosureReason={ClosureReason} NewSessionId={NewSessionId} Action={Action}",
            customerId,
            command,
            current?.Id,
            current?.Status,
            current?.ClosureReason,
            newSessionId,
            action);
    }

    private static SessionLifecycleResult Result(string action, string message, ConversationSession? session = null)
        => new()
        {
            Action = action,
            Message = message,
            Session = session?.ToDto()
        };
}
