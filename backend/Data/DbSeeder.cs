using Cia.Api.Configuration;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Data;

public static class DbSeeder
{
    public const string DemoCustomerId = "CLIENTE-001";
    public const string PedroCustomerId = "PEDRO-001";
    public const string RafaelCustomerId = "RAFAEL-001";

    public const string DemoCustomerEmail = "lucas@claro.com";
    public const string PedroEmail = "pedro@claro.com";
    public const string RafaelEmail = "rafael@claro.com";
    public const string DemoAgentEmail = "agente@claro.com";
    public const string DemoAdminEmail = "admin@claro.com";

    public static async Task SeedAsync(
        AppDbContext db,
        DemoUsersOptions? demoUsers = null,
        bool isProduction = false,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        demoUsers ??= new DemoUsersOptions();

        await EnsureCustomerAsync(db, DemoCustomerId, "Lucas", "11999999999", cancellationToken);
        await EnsureCustomerAsync(db, PedroCustomerId, "Pedro", "11988887777", cancellationToken);
        await EnsureCustomerAsync(db, RafaelCustomerId, "Rafael", "11977776666", cancellationToken);

        await EnsureDemoCustomerUserAsync(
            db, PedroEmail, "Pedro", PedroCustomerId, demoUsers.Pedro?.Password, isProduction, logger, cancellationToken);
        await EnsureDemoCustomerUserAsync(
            db, DemoCustomerEmail, "Lucas", DemoCustomerId, demoUsers.Lucas?.Password, isProduction, logger, cancellationToken);
        await EnsureDemoCustomerUserAsync(
            db, RafaelEmail, "Rafael", RafaelCustomerId, demoUsers.Rafael?.Password, isProduction, logger, cancellationToken);

        await EnsureStaffUserAsync(
            db,
            DemoAgentEmail,
            "Ana Souza",
            UserRole.Agent,
            demoUsers.Agent?.Password,
            isProduction,
            logger,
            cancellationToken);
        await EnsureStaffUserAsync(
            db,
            DemoAdminEmail,
            "Admin CIA",
            UserRole.Admin,
            demoUsers.Admin?.Password,
            isProduction,
            logger,
            cancellationToken);

        await BackfillTelegramIdentitiesAsync(db, cancellationToken);
    }

    private static async Task EnsureCustomerAsync(
        AppDbContext db,
        string id,
        string name,
        string phone,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            db.Customers.Add(new Customer
            {
                Id = id,
                Name = name,
                Phone = phone,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!string.Equals(customer.Name, name, StringComparison.Ordinal))
        {
            customer.Name = name;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureDemoCustomerUserAsync(
        AppDbContext db,
        string email,
        string name,
        string customerId,
        string? password,
        bool isProduction,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (!hasPassword)
        {
            if (isProduction)
            {
                logger?.LogWarning(
                    "Demo customer credential was not configured. Login for {Email} was not created or updated.",
                    normalized);
            }
            else
            {
                logger?.LogInformation(
                    "Demo customer credential was not configured. Login for {Email} was not created or updated.",
                    normalized);
            }

            if (user is null)
            {
                return;
            }
        }

        if (user is null)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = normalized,
                PasswordHash = PasswordProtector.Hash(password!),
                Role = UserRole.Customer,
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var changed = false;
        if (!string.Equals(user.Name, name, StringComparison.Ordinal))
        {
            user.Name = name;
            changed = true;
        }

        if (user.Role != UserRole.Customer)
        {
            user.Role = UserRole.Customer;
            changed = true;
        }

        if (!string.Equals(user.CustomerId, customerId, StringComparison.Ordinal))
        {
            user.CustomerId = customerId;
            changed = true;
        }

        if (hasPassword && !PasswordProtector.Verify(password!, user.PasswordHash))
        {
            user.PasswordHash = PasswordProtector.Hash(password!);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureStaffUserAsync(
        AppDbContext db,
        string email,
        string name,
        UserRole role,
        string? password,
        bool isProduction,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        var hasPassword = !string.IsNullOrWhiteSpace(password);
        if (!hasPassword)
        {
            var level = isProduction ? LogLevel.Warning : LogLevel.Information;
            logger?.Log(
                level,
                "Demo staff credential was not configured. Login for {Email} was not created or updated.",
                normalized);
            return;
        }

        if (user is null)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = normalized,
                PasswordHash = PasswordProtector.Hash(password!),
                Role = role,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var changed = false;
        if (!string.Equals(user.Name, name, StringComparison.Ordinal))
        {
            user.Name = name;
            changed = true;
        }

        if (user.Role != role)
        {
            user.Role = role;
            changed = true;
        }

        if (!PasswordProtector.Verify(password!, user.PasswordHash))
        {
            user.PasswordHash = PasswordProtector.Hash(password!);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task BackfillTelegramIdentitiesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var customers = await db.Customers
            .Where(c => c.TelegramUserId != null)
            .ToListAsync(cancellationToken);

        foreach (var customer in customers)
        {
            var externalUserId = customer.TelegramUserId!.Value.ToString();
            var exists = await db.CustomerChannelIdentities.AnyAsync(
                x => x.Channel == ChannelType.Telegram && x.ExternalUserId == externalUserId,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            db.CustomerChannelIdentities.Add(new CustomerChannelIdentity
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Channel = ChannelType.Telegram,
                ExternalUserId = externalUserId,
                ExternalChatId = customer.TelegramChatId?.ToString(),
                DisplayName = customer.Name,
                CreatedAt = customer.CreatedAt
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
