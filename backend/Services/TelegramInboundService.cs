using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class TelegramInboundService : ITelegramInboundService
{
    public const int MaxMessageLength = 2000;

    private readonly ICustomerRepository _customers;
    private readonly IConversationService _conversations;
    private readonly ITelegramService _telegram;
    private readonly ILogger<TelegramInboundService> _logger;

    public TelegramInboundService(
        ICustomerRepository customers,
        IConversationService conversations,
        ITelegramService telegram,
        ILogger<TelegramInboundService> logger)
    {
        _customers = customers;
        _conversations = conversations;
        _telegram = telegram;
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

        var customer = await GetOrCreateCustomerAsync(telegramUserId, telegramChatId, firstName, cancellationToken);
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
        if (last is { Sender: MessageSender.Assistant })
        {
            await _telegram.SendMessageAsync(telegramChatId, last.Content, cancellationToken);
        }
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken)
    {
        var existing = await _customers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (existing is not null)
        {
            var name = ResolveName(firstName, telegramUserId);
            var changed = false;
            if (!string.Equals(existing.Name, name, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(firstName))
            {
                existing.Name = name;
                changed = true;
            }

            if (existing.TelegramChatId != telegramChatId)
            {
                existing.TelegramChatId = telegramChatId;
                changed = true;
            }

            if (changed)
            {
                await _customers.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var customer = new Customer
        {
            Id = $"TG-{telegramUserId}",
            Name = ResolveName(firstName, telegramUserId),
            Phone = TruncatePhone(telegramUserId.ToString()),
            TelegramUserId = telegramUserId,
            TelegramChatId = telegramChatId,
            CreatedAt = DateTime.UtcNow
        };

        await _customers.AddAsync(customer, cancellationToken);
        await _customers.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private static string ResolveName(string? firstName, long telegramUserId)
    {
        return string.IsNullOrWhiteSpace(firstName) ? $"Cliente {telegramUserId}" : firstName.Trim();
    }

    private static string Truncate(string text)
    {
        var value = text.Trim();
        return value.Length <= MaxMessageLength ? value : value[..MaxMessageLength];
    }

    private static string TruncatePhone(string value)
    {
        return value.Length <= 20 ? value : value[..20];
    }
}
