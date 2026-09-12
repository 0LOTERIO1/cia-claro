using Cia.Api.Interfaces;

namespace Cia.Api.Tests;

internal sealed class FakeTelegramService : ITelegramService
{
    public bool IsConfigured { get; set; } = true;
    public List<(long ChatId, string Text)> Sent { get; } = new();

    public Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        Sent.Add((chatId, text));
        return Task.CompletedTask;
    }
}
