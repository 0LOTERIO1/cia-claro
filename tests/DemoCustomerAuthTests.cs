using Cia.Api.Configuration;
using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Cia.Api.Tests;

public class DemoCustomerAuthTests
{
    [Fact]
    public async Task ThreeCustomers_CanLogin_AndResolveToDifferentIdentities()
    {
        using var db = TestComposition.CreateDb();
        var passwords = await SeedWithGeneratedPasswordsAsync(db);
        var auth = CreateAuth(db);

        var pedro = await CompleteInitialLoginAsync(auth, DbSeeder.PedroEmail, passwords.Pedro.Password!);
        var lucas = await CompleteInitialLoginAsync(auth, DbSeeder.DemoCustomerEmail, passwords.Lucas.Password!);
        var rafael = await CompleteInitialLoginAsync(auth, DbSeeder.RafaelEmail, passwords.Rafael.Password!);

        Assert.Equal(UserRole.Customer, pedro.User.Role);
        Assert.Equal(UserRole.Customer, lucas.User.Role);
        Assert.Equal(UserRole.Customer, rafael.User.Role);
        Assert.Equal("Pedro", pedro.User.Name);
        Assert.Equal("Lucas", lucas.User.Name);
        Assert.Equal("Rafael", rafael.User.Name);
        Assert.Equal(DbSeeder.PedroCustomerId, pedro.User.CustomerId);
        Assert.Equal(DbSeeder.DemoCustomerId, lucas.User.CustomerId);
        Assert.Equal(DbSeeder.RafaelCustomerId, rafael.User.CustomerId);
        Assert.NotEqual(pedro.User.CustomerId, lucas.User.CustomerId);
        Assert.NotEqual(lucas.User.CustomerId, rafael.User.CustomerId);
        Assert.NotEqual(pedro.User.CustomerId, rafael.User.CustomerId);
        Assert.False(string.IsNullOrWhiteSpace(pedro.Token));
        Assert.Equal(3, db.Users.Count(u => u.Role == UserRole.Customer));
    }

    [Fact]
    public async Task ExistingCustomerSeed_IsUpdatedWithoutBreakingLucasIdentity()
    {
        using var db = TestComposition.CreateDb();
        var options = CreateGeneratedOptions();
        await DbSeeder.SeedAsync(db, options);
        await DbSeeder.SeedAsync(db, options);

        Assert.Equal("Lucas", db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).Name);
        Assert.Equal(DbSeeder.DemoCustomerId, db.Users.Single(u => u.Email == DbSeeder.DemoCustomerEmail).CustomerId);
        Assert.Equal(1, db.Users.Count(u => u.Email == DbSeeder.DemoCustomerEmail));
        Assert.Equal(1, db.Users.Count(u => u.Email == DbSeeder.PedroEmail));
        Assert.Equal(1, db.Users.Count(u => u.Email == DbSeeder.RafaelEmail));
        Assert.True(db.Users.Any(u => u.Email == DbSeeder.DemoAgentEmail && u.Role == UserRole.Agent));
        Assert.True(db.Users.Any(u => u.Email == DbSeeder.DemoAdminEmail && u.Role == UserRole.Admin));
    }

    [Fact]
    public async Task PedroDoesNotAccessLucasSession_AndLucasDoesNotAccessRafaelSession()
    {
        using var db = TestComposition.CreateDb();
        await SeedWithGeneratedPasswordsAsync(db);
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var identities = TestComposition.CreateIdentities(db);

        var lucasChat = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var rafaelChat = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.RafaelCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Preciso de ajuda com a fatura."
        });

        var pedroSnapshot = await identities.GetActiveSessionAsync(DbSeeder.PedroCustomerId);
        var lucasSnapshot = await identities.GetActiveSessionAsync(DbSeeder.DemoCustomerId);
        var rafaelSnapshot = await identities.GetActiveSessionAsync(DbSeeder.RafaelCustomerId);

        Assert.Null(pedroSnapshot.Session);
        Assert.Equal(lucasChat.SessionId, lucasSnapshot.Session?.Id);
        Assert.Equal(rafaelChat.SessionId, rafaelSnapshot.Session?.Id);
        Assert.NotEqual(lucasSnapshot.Session?.Id, rafaelSnapshot.Session?.Id);
        Assert.DoesNotContain(lucasSnapshot.Messages, m => m.SessionId == rafaelChat.SessionId);
        Assert.Empty(await conversation.GetSessionsByCustomerAsync(DbSeeder.PedroCustomerId));
        Assert.DoesNotContain(
            await conversation.GetSessionsByCustomerAsync(DbSeeder.PedroCustomerId),
            s => s.Id == lucasChat.SessionId);

        var pedroChat = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.PedroCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero abrir um atendimento."
        });

        Assert.NotEqual(lucasChat.SessionId, pedroChat.SessionId);
        Assert.NotEqual(rafaelChat.SessionId, pedroChat.SessionId);
        Assert.Equal(DbSeeder.PedroCustomerId, db.ConversationSessions.Single(s => s.Id == pedroChat.SessionId).CustomerId);
    }

    [Fact]
    public async Task EachCustomer_CanLinkOwnTelegram_AndIdentityCannotBeShared()
    {
        using var db = TestComposition.CreateDb();
        await SeedWithGeneratedPasswordsAsync(db);
        var identities = TestComposition.CreateIdentities(db);

        var pedroCode = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.PedroCustomerId);
        var lucasCode = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        await identities.RedeemTelegramLinkAsync(pedroCode.Code, 111111, 111111, "Pedro");
        await identities.RedeemTelegramLinkAsync(lucasCode.Code, 222222, 222222, "Lucas");

        var steal = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.RafaelCustomerId);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            identities.RedeemTelegramLinkAsync(steal.Code, 111111, 111111, "Rafael"));
        Assert.Contains("outra conta", ex.Message, StringComparison.OrdinalIgnoreCase);

        var rafaelCode = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.RafaelCustomerId);
        await identities.RedeemTelegramLinkAsync(rafaelCode.Code, 333333, 333333, "Rafael");

        Assert.Equal(DbSeeder.PedroCustomerId, db.CustomerChannelIdentities.Single(i => i.ExternalUserId == "111111").CustomerId);
        Assert.Equal(DbSeeder.DemoCustomerId, db.CustomerChannelIdentities.Single(i => i.ExternalUserId == "222222").CustomerId);
        Assert.Equal(DbSeeder.RafaelCustomerId, db.CustomerChannelIdentities.Single(i => i.ExternalUserId == "333333").CustomerId);
        Assert.Equal(3, db.CustomerChannelIdentities.Count(i => i.Channel == ChannelType.Telegram));
    }

    [Fact]
    public async Task PasswordsAreStoredHashed_NotInPlainText()
    {
        using var db = TestComposition.CreateDb();
        var passwords = await SeedWithGeneratedPasswordsAsync(db);

        var pedro = db.Users.Single(u => u.Email == DbSeeder.PedroEmail);
        var lucas = db.Users.Single(u => u.Email == DbSeeder.DemoCustomerEmail);
        var rafael = db.Users.Single(u => u.Email == DbSeeder.RafaelEmail);

        Assert.DoesNotContain(passwords.Pedro.Password!, pedro.PasswordHash);
        Assert.DoesNotContain(passwords.Lucas.Password!, lucas.PasswordHash);
        Assert.DoesNotContain(passwords.Rafael.Password!, rafael.PasswordHash);
        Assert.True(PasswordProtector.Verify(passwords.Pedro.Password!, pedro.PasswordHash));
        Assert.True(PasswordProtector.Verify(passwords.Lucas.Password!, lucas.PasswordHash));
        Assert.True(PasswordProtector.Verify(passwords.Rafael.Password!, rafael.PasswordHash));
        Assert.Contains('.', pedro.PasswordHash);
    }

    [Fact]
    public async Task Production_WithoutDemoPasswords_DoesNotCreateCustomerLogins()
    {
        using var db = TestComposition.CreateDb();
        await DbSeeder.SeedAsync(db, new DemoUsersOptions(), isProduction: true, NullLogger.Instance);

        Assert.Empty(db.Users.Where(u => u.Role == UserRole.Customer));
        Assert.Contains(db.Customers, c => c.Id == DbSeeder.PedroCustomerId);
        Assert.Contains(db.Customers, c => c.Id == DbSeeder.DemoCustomerId);
        Assert.Contains(db.Customers, c => c.Id == DbSeeder.RafaelCustomerId);
        Assert.False(db.Users.Any(u => u.Email == DbSeeder.DemoAgentEmail));
        Assert.False(db.Users.Any(u => u.Email == DbSeeder.DemoAdminEmail));
    }

    [Fact]
    public void ConfigurationBinding_ReadsEnvironmentStyleKeys()
    {
        var generated = CreateGeneratedOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoUsers:Pedro:Password"] = generated.Pedro.Password,
                ["DemoUsers:Lucas:Password"] = generated.Lucas.Password,
                ["DemoUsers:Rafael:Password"] = generated.Rafael.Password
            })
            .Build();

        var bound = configuration.GetSection(DemoUsersOptions.SectionName).Get<DemoUsersOptions>();
        Assert.NotNull(bound);
        Assert.Equal(generated.Pedro.Password, bound.Pedro.Password);
        Assert.Equal(generated.Lucas.Password, bound.Lucas.Password);
        Assert.Equal(generated.Rafael.Password, bound.Rafael.Password);
    }

    private static async Task<DemoUsersOptions> SeedWithGeneratedPasswordsAsync(AppDbContext db)
    {
        var options = CreateGeneratedOptions();
        await DbSeeder.SeedAsync(db, options, isProduction: false, NullLogger.Instance);
        return options;
    }

    private static DemoUsersOptions CreateGeneratedOptions()
    {
        return new DemoUsersOptions
        {
            Pedro = new DemoUserCredentials { Password = CreateTestSecret() },
            Lucas = new DemoUserCredentials { Password = CreateTestSecret() },
            Rafael = new DemoUserCredentials { Password = CreateTestSecret() },
            Agent = new DemoUserCredentials { Password = CreateTestSecret() },
            Admin = new DemoUserCredentials { Password = CreateTestSecret() }
        };
    }

    private static string CreateTestSecret() => $"X{Guid.NewGuid():N}a1!";

    private static AuthService CreateAuth(AppDbContext db)
    {
        return TestComposition.CreateAuth(db);
    }

    private static async Task<LoginResponse> CompleteInitialLoginAsync(
        AuthService auth,
        string email,
        string password)
    {
        var attempt = await auth.LoginAsync(new LoginRequest { Email = email, Password = password });
        Assert.Equal("requiresSetup", attempt.Status);
        Assert.NotNull(attempt.ManualKey);

        var totp = new Totp(Base32Encoding.ToBytes(attempt.ManualKey!));
        return await auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
        {
            ChallengeId = attempt.ChallengeId,
            Code = totp.ComputeTotp(DateTime.UtcNow)
        });
    }
}
