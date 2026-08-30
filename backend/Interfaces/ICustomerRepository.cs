using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
