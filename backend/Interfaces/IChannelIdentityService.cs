using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.Interfaces;

public interface IChannelIdentityService
{
    Task<Customer> GetOrCreateTelegramCustomerAsync(
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken = default);

    Task<long?> GetTelegramChatIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerChannelDto>> ListChannelsAsync(string customerId, CancellationToken cancellationToken = default);

    Task<TelegramLinkCodeDto> GenerateTelegramLinkCodeAsync(string customerId, CancellationToken cancellationToken = default);

    Task<string> RedeemTelegramLinkAsync(
        string code,
        long telegramUserId,
        long telegramChatId,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<ActiveSessionResponse> GetActiveSessionAsync(string customerId, CancellationToken cancellationToken = default);

    Task<ActiveSessionResponse> ResumeActiveSessionAsync(string customerId, CancellationToken cancellationToken = default);
}
