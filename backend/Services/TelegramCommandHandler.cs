using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class TelegramCommandHandler
{
    public const string HumanSessionMessage = SessionLifecycleService.HumanSessionMessage;
    public const string OfferRestartMessage = SessionLifecycleService.OfferRestartMessage;
    public const string ContinuedMessage = SessionLifecycleService.ContinuedMessage;
    public const string EndedMessage = SessionLifecycleService.EndedMessage;
    public const string HumanEndMessage = SessionLifecycleService.HumanEndMessage;

    private readonly ISessionLifecycleService _lifecycle;
    private readonly ITelegramService _telegram;
    private readonly ILogger<TelegramCommandHandler> _logger;

    public TelegramCommandHandler(
        ISessionLifecycleService lifecycle,
        ITelegramService telegram,
        ILogger<TelegramCommandHandler> logger)
    {
        _lifecycle = lifecycle;
        _telegram = telegram;
        _logger = logger;
    }

    public Task HandleStartAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken = default)
        => DispatchAsync("start", customer, telegramUserId, telegramChatId, firstName, cancellationToken);

    public Task HandleContinueAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        CancellationToken cancellationToken = default)
        => DispatchAsync("continuar", customer, telegramUserId, telegramChatId, null, cancellationToken);

    public Task HandleRestartAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken = default)
        => DispatchAsync("novo", customer, telegramUserId, telegramChatId, firstName, cancellationToken);

    public Task HandleEndAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        CancellationToken cancellationToken = default)
        => DispatchAsync("encerrar", customer, telegramUserId, telegramChatId, null, cancellationToken);

    public static string BuildGreeting(string? firstName, string? customerName)
    {
        var name = ResolveDisplayName(firstName, customerName);
        return string.IsNullOrWhiteSpace(name)
            ? "Olá! Sou a CIA, assistente virtual da Claro. Como posso ajudar você hoje?"
            : $"Olá, {name}! Sou a CIA, assistente virtual da Claro. Como posso ajudar você hoje?";
    }

    private async Task DispatchAsync(
        string command,
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken)
    {
        var result = command switch
        {
            "start" => await _lifecycle.HandleStartAsync(customer.Id, ChannelType.Telegram, firstName, cancellationToken),
            "continuar" => await _lifecycle.ContinueAsync(customer.Id, ChannelType.Telegram, cancellationToken),
            "novo" => await _lifecycle.RestartAsync(customer.Id, ChannelType.Telegram, firstName, cancellationToken),
            "encerrar" => await _lifecycle.EndAsync(customer.Id, cancellationToken),
            _ => throw new InvalidOperationException($"Comando Telegram não suportado: {command}")
        };

        _logger.LogInformation(
            "Telegram command received. TelegramUserId={TelegramUserId} Command={Command} SessionId={SessionId} Action={Action} ClosedSessionId={ClosedSessionId} NewSessionId={NewSessionId}",
            telegramUserId, command, result.Session?.Id, result.Action, result.ClosedSessionId, result.NewSessionId);

        await _telegram.SendMessageAsync(telegramChatId, result.Message, cancellationToken);
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
        return name.StartsWith("Cliente ", StringComparison.OrdinalIgnoreCase) ? null : name;
    }
}
