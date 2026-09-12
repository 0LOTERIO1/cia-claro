using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class TelegramInboundService : ITelegramInboundService
{
    public const int MaxMessageLength = 2000;

    private readonly IConversationService _conversations;
    private readonly ITelegramService _telegram;
    private readonly IChannelIdentityService _identities;
    private readonly ILogger<TelegramInboundService> _logger;

    public TelegramInboundService(
        IConversationService conversations,
        ITelegramService telegram,
        IChannelIdentityService identities,
        ILogger<TelegramInboundService> logger)
    {
        _conversations = conversations;
        _telegram = telegram;
        _identities = identities;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(TelegramUpdateDto update, CancellationToken cancellationToken = default)
    {
        var message = update.Message;
        if (message is null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        if (message.From is null || message.Chat is null || message.From.IsBot)
        {
            return;
        }

        var telegramChatId = message.Chat.Id;
        var telegramUserId = message.From.Id;
        var firstName = message.From.FirstName;
        var username = message.From.Username;
        var messageText = Truncate(message.Text);

        _logger.LogInformation(
            "Telegram update received. UpdateId={UpdateId} ChatId={ChatId} UserId={UserId} Username={Username}",
            update.UpdateId, telegramChatId, telegramUserId, username);

        if (TryParseLinkCommand(messageText, out var linkCode))
        {
            await HandleLinkCommandAsync(linkCode, telegramUserId, telegramChatId, firstName, cancellationToken);
            return;
        }

        var customer = await _identities.GetOrCreateTelegramCustomerAsync(
            telegramUserId,
            telegramChatId,
            firstName,
            cancellationToken);
        _logger.LogInformation(
            "Telegram customer identified. CustomerId={CustomerId} Name={Name}",
            customer.Id, customer.Name);

        var response = await _conversations.SendMessageAsync(
            new SendMessageRequest
            {
                CustomerId = customer.Id,
                Channel = ChannelType.Telegram,
                Content = messageText
            },
            cancellationToken);

        _logger.LogInformation(
            "Telegram message processed. Protocol={Protocol} Status={Status} Intent={Intent}",
            response.Protocol, response.Status, response.DetectedIntent);

        var last = response.Messages.LastOrDefault();
        if (last is { Sender: MessageSender.Assistant } && response.CurrentChannel == ChannelType.Telegram)
        {
            await _telegram.SendMessageAsync(telegramChatId, last.Content, cancellationToken);
        }
    }

    private async Task HandleLinkCommandAsync(
        string code,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken)
    {
        try
        {
            var confirmation = await _identities.RedeemTelegramLinkAsync(
                code,
                telegramUserId,
                telegramChatId,
                firstName,
                cancellationToken);
            await _telegram.SendMessageAsync(telegramChatId, confirmation, cancellationToken);
        }
        catch (Exception ex) when (ex is ValidationAppException or ConflictException or NotFoundException)
        {
            _logger.LogWarning(
                "Telegram link command rejected. UserId={UserId} Reason={Reason}",
                telegramUserId, ex.Message);
            await _telegram.SendMessageAsync(telegramChatId, ex.Message, cancellationToken);
        }
    }

    public static bool TryParseLinkCommand(string text, out string code)
    {
        code = string.Empty;
        var value = text.Trim();
        if (!value.StartsWith("/link", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = value[5..].Trim();
        if (rest.StartsWith('@'))
        {
            var space = rest.IndexOf(' ');
            rest = space < 0 ? string.Empty : rest[(space + 1)..].Trim();
        }

        code = rest;
        return true;
    }

    private static string Truncate(string text)
    {
        var value = text.Trim();
        return value.Length <= MaxMessageLength ? value : value[..MaxMessageLength];
    }
}
