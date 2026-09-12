using System.Net.Http.Json;
using Cia.Api.Configuration;
using Cia.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Cia.Api.Services;

public class TelegramService : ITelegramService
{
    private readonly TelegramOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(
        IOptions<TelegramOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("Telegram send skipped because the integration is not configured. ChatId={ChatId}", chatId);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var client = _httpClientFactory.CreateClient("Telegram");
        var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
        using var response = await client.PostAsJsonAsync(
            url,
            new { chat_id = chatId, text },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Telegram API rejected sendMessage. ChatId={ChatId} Status={Status}",
                chatId, (int)response.StatusCode);
            return;
        }

        _logger.LogInformation("Telegram response sent. ChatId={ChatId}", chatId);
    }
}
