using Cia.Api.DTOs;
using Cia.Api.Enums;

namespace Cia.Api.Interfaces;

public interface ISessionLifecycleService
{
    Task<SessionLifecycleResult> HandleStartAsync(
        string customerId,
        ChannelType channel,
        string? firstName = null,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> ContinueAsync(
        string customerId,
        ChannelType channel,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> RestartAsync(
        string customerId,
        ChannelType channel,
        string? firstName = null,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> EndAsync(
        string customerId,
        CancellationToken cancellationToken = default);
}
