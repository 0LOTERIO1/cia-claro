using Cia.Api.DTOs;
using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IRegionalOutageRepository
{
    Task<IReadOnlyList<RegionalOutage>> ListAsync(
        bool includeResolved,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionalOutage>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<RegionalOutage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(RegionalOutage outage, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRegionalOutageService
{
    Task<RegionalOutageCheckResponse> CheckAsync(
        string postalCode,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionalOutageDto>> ListAsync(
        bool includeResolved,
        CancellationToken cancellationToken = default);
    Task<RegionalOutageDto> CreateAsync(
        Guid adminUserId,
        CreateRegionalOutageRequest request,
        CancellationToken cancellationToken = default);
    Task<RegionalOutageDto> ResolveAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
