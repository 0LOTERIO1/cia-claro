using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class TelegramCommandHandler
{
    public const string ActiveSessionMessage =
        "Você já possui um atendimento em andamento. Pode continuar por aqui.";

    public const string HumanSessionMessage =
        "Você já possui um atendimento com nossa equipe em andamento.";

    private readonly ISessionRepository _sessions;
    private readonly IConversationService _conversations;
    private readonly IMessageRepository _messages;
    private readonly ITelegramService _telegram;
    private readonly ILogger<TelegramCommandHandler> _logger;

    public TelegramCommandHandler(
        ISessionRepository sessions,
        IConversationService conversations,
        IMessageRepository messages,
        ITelegramService telegram,
        ILogger<TelegramCommandHandler> logger)
    {
        _sessions = sessions;
        _conversations = conversations;
        _messages = messages;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task HandleStartAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken = default)
    {
        var open = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (open is not null && IsHumanSession(open))
        {
            LogCommand(telegramUserId, "start", open.Id, TelegramCommandActions.HumanSessionPreserved);
            await _telegram.SendMessageAsync(telegramChatId, HumanSessionMessage, cancellationToken);
            return;
        }

        if (open is not null)
        {
            if (open.CurrentChannel != ChannelType.Telegram)
            {
                open.CurrentChannel = ChannelType.Telegram;
                open.UpdatedAt = DateTime.UtcNow;
                await _sessions.SaveChangesAsync(cancellationToken);
            }

            LogCommand(telegramUserId, "start", open.Id, TelegramCommandActions.ContinuedActiveSession);
            await _telegram.SendMessageAsync(telegramChatId, ActiveSessionMessage, cancellationToken);
            return;
        }

        var created = await _conversations.CreateSessionAsync(
            new CreateSessionRequest
            {
                CustomerId = customer.Id,
                Channel = ChannelType.Telegram
            },
            cancellationToken);

        var greeting = BuildGreeting(firstName, customer.Name);
        await _messages.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            SessionId = created.Id,
            Sender = MessageSender.Assistant,
            Channel = ChannelType.Telegram,
            Content = greeting,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);

        LogCommand(telegramUserId, "start", created.Id, TelegramCommandActions.CreatedNewSession);
        await _telegram.SendMessageAsync(telegramChatId, greeting, cancellationToken);
    }

    public static string BuildGreeting(string? firstName, string? customerName)
    {
        var name = ResolveDisplayName(firstName, customerName);
        return string.IsNullOrWhiteSpace(name)
            ? "Olá! Sou a CIA, assistente virtual da Claro. Como posso ajudar você hoje?"
            : $"Olá, {name}! Sou a CIA, assistente virtual da Claro. Como posso ajudar você hoje?";
    }

    private static string? ResolveDisplayName(string? firstName, string? customerName)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            return firstName.Trim();
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return null;
        }

        var name = customerName.Trim();
        if (name.StartsWith("Cliente ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return name;
    }

    private static bool IsHumanSession(ConversationSession session)
    {
        if (session.Status is SessionStatus.WaitingForAgent or SessionStatus.Transferred)
        {
            return true;
        }

        var requestStatus = session.HumanAgentRequests?
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (HumanAgentRequestStatus?)r.Status)
            .FirstOrDefault();

        return requestStatus is HumanAgentRequestStatus.Waiting or HumanAgentRequestStatus.Assigned;
    }

    private void LogCommand(long telegramUserId, string command, Guid? sessionId, string action)
    {
        _logger.LogInformation(
            "Telegram command received. TelegramUserId={TelegramUserId} Command={Command} SessionId={SessionId} Action={Action}",
            telegramUserId, command, sessionId, action);
    }
}
