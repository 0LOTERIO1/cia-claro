using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.Interfaces;

public interface IChannelIdentityRepository
{
    Task<CustomerChannelIdentity?> GetByChannelUserAsync(
        ChannelType channel,
        string externalUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerChannelIdentity>> GetByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CustomerChannelIdentity identity, CancellationToken cancellationToken = default);
}

public interface IChannelLinkCodeRepository
{
    Task<IReadOnlyList<ChannelLinkCode>> GetPendingAsync(
        string customerId,
        ChannelType channel,
        CancellationToken cancellationToken = default);

    Task<ChannelLinkCode?> GetLatestByHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task AddAsync(ChannelLinkCode code, CancellationToken cancellationToken = default);
}
