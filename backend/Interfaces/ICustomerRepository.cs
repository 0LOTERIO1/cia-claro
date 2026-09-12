using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Customer?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
