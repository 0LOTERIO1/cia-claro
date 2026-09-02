using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Data;

public static class DbSeeder
{
    public const string DemoCustomerId = "CLIENTE-001";
    public const string DemoPassword = "Claro@123";
    public const string DemoCustomerEmail = "lucas@claro.com";
    public const string DemoAgentEmail = "agente@claro.com";
    public const string DemoAdminEmail = "admin@claro.com";

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == DemoCustomerId, cancellationToken))
        {
            db.Customers.Add(new Customer
            {
                Id = DemoCustomerId,
                Name = "Lucas",
                Phone = "11999999999",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Lucas",
                Email = DemoCustomerEmail,
                PasswordHash = PasswordProtector.Hash(DemoPassword),
                Role = UserRole.Customer,
                CustomerId = DemoCustomerId,
                CreatedAt = now
            },
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Ana Souza",
                Email = DemoAgentEmail,
                PasswordHash = PasswordProtector.Hash(DemoPassword),
                Role = UserRole.Agent,
                CreatedAt = now
            },
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Admin CIA",
                Email = DemoAdminEmail,
                PasswordHash = PasswordProtector.Hash(DemoPassword),
                Role = UserRole.Admin,
                CreatedAt = now
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
