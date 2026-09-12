using Cia.Api.DTOs;

namespace Cia.Api.Interfaces;

public interface ITelegramService
{
    bool IsConfigured { get; }
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);
}

public interface ITelegramInboundService
{
    Task HandleUpdateAsync(TelegramUpdateDto update, CancellationToken cancellationToken = default);
}
