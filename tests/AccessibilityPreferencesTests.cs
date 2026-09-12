using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cia.Api.Tests;

public class AccessibilityPreferencesTests
{
    [Fact]
    public async Task Customer_ReadsDefaultPreferences()
    {
        using var db = TestComposition.CreateDb();
        var customer = SeedUser(db, UserRole.Customer, DbSeeder.DemoCustomerId);
        var service = CreateService(db);

        var prefs = await service.GetMineAsync(customer.Id);

        Assert.Equal(1.0, prefs.FontScale);
        Assert.False(prefs.HighContrast);
        Assert.Equal(AccessibilityTheme.System, prefs.Theme);
        Assert.False(prefs.ReducedMotion);
        Assert.Equal(ReadingSpacing.Normal, prefs.ReadingSpacing);
        Assert.False(prefs.ReadAloudEnabled);
    }

    [Fact]
    public async Task Agent_ReadsOwnPreferences()
    {
        using var db = TestComposition.CreateDb();
        var agent = SeedUser(db, UserRole.Agent);
        var service = CreateService(db);

        var prefs = await service.GetMineAsync(agent.Id);

        Assert.Equal(AccessibilityTheme.System, prefs.Theme);
        Assert.Equal(1, db.AccessibilityPreferences.Count(p => p.UserId == agent.Id));
    }

    [Fact]
    public async Task Admin_ReadsOwnPreferences()
    {
        using var db = TestComposition.CreateDb();
        var admin = SeedUser(db, UserRole.Admin);
        var service = CreateService(db);

        var prefs = await service.GetMineAsync(admin.Id);

        Assert.Equal(1.0, prefs.FontScale);
        Assert.Equal(admin.Id, db.AccessibilityPreferences.Single().UserId);
    }

    [Fact]
    public async Task Customer_UpdatesOnlyOwnPreferences()
    {
        using var db = TestComposition.CreateDb();
        db.Customers.Add(new Customer
        {
            Id = DbSeeder.PedroCustomerId,
            Name = "Pedro",
            Phone = "11988887777",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var lucas = SeedUser(db, UserRole.Customer, DbSeeder.DemoCustomerId, "lucas-a11y@claro.com");
        var pedro = SeedUser(db, UserRole.Customer, DbSeeder.PedroCustomerId, "pedro-a11y@claro.com");
        var service = CreateService(db);

        await service.UpdateMineAsync(lucas.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.5,
            HighContrast = true,
            Theme = AccessibilityTheme.Dark,
            ReducedMotion = true,
            ReadingSpacing = ReadingSpacing.Expanded,
            ReadAloudEnabled = true
        });

        var lucasPrefs = await service.GetMineAsync(lucas.Id);
        var pedroPrefs = await service.GetMineAsync(pedro.Id);

        Assert.Equal(1.5, lucasPrefs.FontScale);
        Assert.True(lucasPrefs.HighContrast);
        Assert.Equal(AccessibilityTheme.Dark, lucasPrefs.Theme);
        Assert.Equal(1.0, pedroPrefs.FontScale);
        Assert.False(pedroPrefs.HighContrast);
        Assert.Equal(AccessibilityTheme.System, pedroPrefs.Theme);
    }

    [Fact]
    public async Task Agent_CannotChangeAnotherUsersPreferences()
    {
        using var db = TestComposition.CreateDb();
        var agent = SeedUser(db, UserRole.Agent);
        var customer = SeedUser(db, UserRole.Customer, DbSeeder.DemoCustomerId);
        var service = CreateService(db);

        await service.UpdateMineAsync(customer.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.3,
            Theme = AccessibilityTheme.Light,
            ReadingSpacing = ReadingSpacing.Comfortable
        });

        await service.UpdateMineAsync(agent.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.15,
            HighContrast = true,
            Theme = AccessibilityTheme.Dark,
            ReadingSpacing = ReadingSpacing.Expanded,
            ReadAloudEnabled = true
        });

        var customerPrefs = await service.GetMineAsync(customer.Id);
        var agentPrefs = await service.GetMineAsync(agent.Id);

        Assert.Equal(1.3, customerPrefs.FontScale);
        Assert.Equal(AccessibilityTheme.Light, customerPrefs.Theme);
        Assert.False(customerPrefs.HighContrast);
        Assert.Equal(1.15, agentPrefs.FontScale);
        Assert.True(agentPrefs.HighContrast);
        Assert.NotEqual(customerPrefs.FontScale, agentPrefs.FontScale);
    }

    [Fact]
    public async Task InvalidValues_AreRejected()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db, UserRole.Customer, DbSeeder.DemoCustomerId);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateMineAsync(user.Id, new AccessibilityPreferencesDto
        {
            FontScale = 2.0,
            Theme = AccessibilityTheme.System,
            ReadingSpacing = ReadingSpacing.Normal
        }));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateMineAsync(user.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.0,
            Theme = (AccessibilityTheme)99,
            ReadingSpacing = ReadingSpacing.Normal
        }));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateMineAsync(user.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.0,
            Theme = AccessibilityTheme.System,
            ReadingSpacing = (ReadingSpacing)99
        }));

        var current = await service.GetMineAsync(user.Id);
        Assert.Equal(1.0, current.FontScale);
        Assert.Equal(AccessibilityTheme.System, current.Theme);
    }

    [Fact]
    public async Task Preferences_PersistAfterNewRequest()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db, UserRole.Admin);
        var first = CreateService(db);
        await first.UpdateMineAsync(user.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.15,
            HighContrast = true,
            Theme = AccessibilityTheme.Light,
            ReducedMotion = true,
            ReadingSpacing = ReadingSpacing.Comfortable,
            ReadAloudEnabled = true
        });

        var second = CreateService(db);
        var loaded = await second.GetMineAsync(user.Id);

        Assert.Equal(1.15, loaded.FontScale);
        Assert.True(loaded.HighContrast);
        Assert.Equal(AccessibilityTheme.Light, loaded.Theme);
        Assert.True(loaded.ReducedMotion);
        Assert.Equal(ReadingSpacing.Comfortable, loaded.ReadingSpacing);
        Assert.True(loaded.ReadAloudEnabled);
        Assert.Equal(1, db.AccessibilityPreferences.Count(p => p.UserId == user.Id));
    }

    [Fact]
    public async Task AddingPreferences_DoesNotDestroyExistingUsersOrSessions()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var chat = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var user = SeedUser(db, UserRole.Customer, DbSeeder.DemoCustomerId);
        var service = CreateService(db);
        await service.UpdateMineAsync(user.Id, new AccessibilityPreferencesDto
        {
            FontScale = 1.3,
            Theme = AccessibilityTheme.Dark,
            ReadingSpacing = ReadingSpacing.Normal
        });

        Assert.Equal(DbSeeder.DemoCustomerId, db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).Id);
        Assert.Equal(chat.Protocol, db.ConversationSessions.Single().Protocol);
        Assert.True(db.Messages.Any());
        Assert.Equal(chat.SessionId, db.ConversationContexts.Single().SessionId);
        Assert.Equal(user.Id, db.Users.Single(u => u.Email == user.Email).Id);
        Assert.Equal(1.3, db.AccessibilityPreferences.Single().FontScale);
    }

    private static AccessibilityService CreateService(AppDbContext db)
    {
        return new AccessibilityService(
            new UserRepository(db),
            new AccessibilityPreferencesRepository(db),
            NullLogger<AccessibilityService>.Instance);
    }

    private static User SeedUser(AppDbContext db, UserRole role, string? customerId = null, string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = role.ToString(),
            Email = email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@claro.com",
            PasswordHash = "hash",
            Role = role,
            CustomerId = role == UserRole.Customer ? customerId : null,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
