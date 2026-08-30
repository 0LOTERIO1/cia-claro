using Cia.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Data;

public static class DbSeeder
{
    public const string DemoCustomerId = "CLIENTE-001";

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(c => c.Id == DemoCustomerId, cancellationToken))
        {
            return;
        }

        db.Customers.Add(new Customer
        {
            Id = DemoCustomerId,
            Name = "Lucas",
            Phone = "11999999999",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
