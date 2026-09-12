using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IServiceRatingRepository
{
    Task<ServiceRating?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRating>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ServiceRating rating, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IServiceRatingService
{
    Task<DTOs.SubmitServiceRatingResponse> SubmitAsync(
        string customerId,
        Guid sessionId,
        DTOs.SubmitServiceRatingRequest request,
        CancellationToken cancellationToken = default);
}
