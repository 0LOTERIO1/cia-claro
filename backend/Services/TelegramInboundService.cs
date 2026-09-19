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
    private readonly TelegramCommandHandler _commands;
    private readonly TelegramAbuseGuard? _abuseGuard;
    private readonly ILogger<TelegramInboundService> _logger;

    public TelegramInboundService(
        IConversationService conversations,
        ITelegramService telegram,
        IChannelIdentityService identities,
        TelegramCommandHandler commands,
        ILogger<TelegramInboundService> logger,
        TelegramAbuseGuard? abuseGuard = null)
    {
        _conversations = conversations;
        _telegram = telegram;
        _identities = identities;
        _commands = commands;
        _abuseGuard = abuseGuard;
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
        var stage = "received";

        var abuseDecision = _abuseGuard?.Evaluate(update.UpdateId, telegramChatId)
            ?? TelegramUpdateDecision.Allowed;
        if (abuseDecision != TelegramUpdateDecision.Allowed)
        {
            _logger.LogWarning(
                "Telegram update ignored. UpdateId={UpdateId} Decision={Decision}",
                update.UpdateId,
                abuseDecision);
            return;
        }

        _logger.LogInformation(
            "Telegram update received. UpdateId={UpdateId} ChatId={ChatId} UserId={UserId} Username={Username}",
            update.UpdateId, telegramChatId, telegramUserId, username);

        try
        {
            // /link precisa ocorrer ANTES de GetOrCreateTelegramCustomerAsync para adotar a identidade temporária.
            if (TelegramCommandParser.TryParse(messageText, out var command, out var argument) &&
                TelegramCommandParser.IsLink(command))
            {
                stage = "link-command";
                await HandleLinkCommandAsync(argument, telegramUserId, telegramChatId, firstName, cancellationToken);
                return;
            }

            stage = "resolve-customer";
            var customer = await _identities.GetOrCreateTelegramCustomerAsync(
                telegramUserId,
                telegramChatId,
                firstName,
                cancellationToken);
            _logger.LogInformation(
                "Telegram customer identified. CustomerId={CustomerId} Name={Name}",
                customer.Id, customer.Name);

            if (TelegramCommandParser.TryParse(messageText, out command, out _) &&
                TelegramCommandParser.IsSessionLifecycle(command))
            {
                stage = $"{command}-command";
                if (TelegramCommandParser.IsStart(command))
                {
                    await _commands.HandleStartAsync(customer, telegramUserId, telegramChatId, firstName, cancellationToken);
                }
                else if (TelegramCommandParser.IsContinue(command))
                {
                    await _commands.HandleContinueAsync(customer, telegramUserId, telegramChatId, cancellationToken);
                }
                else if (TelegramCommandParser.IsRestart(command))
                {
                    await _commands.HandleRestartAsync(customer, telegramUserId, telegramChatId, firstName, cancellationToken);
                }
                else
                {
                    await _commands.HandleEndAsync(customer, telegramUserId, telegramChatId, cancellationToken);
                }

                return;
            }

            stage = "conversation";
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

            stage = "telegram-reply";
            var last = response.Messages.LastOrDefault();
            if (last is { Sender: MessageSender.Assistant } && response.CurrentChannel == ChannelType.Telegram)
            {
                await _telegram.SendMessageAsync(telegramChatId, last.Content, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram inbound failed. UpdateId={UpdateId} TelegramUserId={TelegramUserId} Stage={Stage}",
                update.UpdateId, telegramUserId, stage);
            throw;
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
        if (!TelegramCommandParser.TryParse(text, out var command, out var argument) ||
            !TelegramCommandParser.IsLink(command))
        {
            return false;
        }

        code = argument;
        return true;
    }

    private static string Truncate(string text)
    {
        var value = text.Trim();
        return value.Length <= MaxMessageLength ? value : value[..MaxMessageLength];
    }
}
