using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cia.Api.Controllers;
using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Services;
using Microsoft.AspNetCore.Authorization;
using OtpNet;

namespace Cia.Api.Tests;

public class TwoFactorAuthTests
{
    private const string Password = "Teste@123456";
    private static readonly DateTimeOffset InitialTime =
        new(2026, 9, 16, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PasswordLogin_ReturnsSetupChallenge_WithoutJwt()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db);
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);

        var attempt = await auth.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = Password
        });

        Assert.Equal("requiresSetup", attempt.Status);
        Assert.NotEqual(Guid.Empty, attempt.ChallengeId);
        Assert.NotNull(attempt.ManualKey);
        Assert.StartsWith("otpauth://totp/", attempt.OtpAuthUri);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Setup_EncryptsSecret_ReturnsRecoveryCodes_AndIssuesMfaJwt()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db);
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);

        var (response, secret) = await SetupAsync(auth, user, clock);

        Assert.True(user.TwoFactorEnabled);
        Assert.NotNull(user.TwoFactorSecretEncrypted);
        Assert.DoesNotContain(secret, user.TwoFactorSecretEncrypted);
        Assert.Equal(10, response.RecoveryCodes?.Count);
        Assert.Equal(10, db.TwoFactorRecoveryCodes.Count());
        Assert.All(db.TwoFactorRecoveryCodes, code => Assert.DoesNotContain("-", code.CodeHash));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Contains(token.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Customer");
    }

    [Fact]
    public async Task EnabledUser_RequiresFreshTotp_AndRejectsReplay()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db);
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);
        var (_, secret) = await SetupAsync(auth, user, clock);

        clock.Advance(TimeSpan.FromSeconds(31));
        var attempt = await LoginAsync(auth, user);
        Assert.Equal("requiresCode", attempt.Status);
        var code = ComputeTotp(secret, clock.GetUtcNow());

        var response = await auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
        {
            ChallengeId = attempt.ChallengeId,
            Code = code
        });
        Assert.False(string.IsNullOrWhiteSpace(response.Token));

        var replayAttempt = await LoginAsync(auth, user);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
            {
                ChallengeId = replayAttempt.ChallengeId,
                Code = code
            }));
    }

    [Fact]
    public async Task Challenge_Expires_AndStopsAfterMaximumAttempts()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db);
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);

        var expired = await LoginAsync(auth, user);
        clock.Advance(TimeSpan.FromMinutes(6));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
            {
                ChallengeId = expired.ChallengeId,
                Code = "000000"
            }));

        var limited = await LoginAsync(auth, user);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
                auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
                {
                    ChallengeId = limited.ChallengeId,
                    Code = "000000"
                }));
        }

        var challenge = db.TwoFactorChallenges.Single(item => item.Id == limited.ChallengeId);
        Assert.Equal(5, challenge.FailedAttempts);
        Assert.NotNull(challenge.ConsumedAt);
    }

    [Fact]
    public async Task RecoveryCode_IsSingleUse()
    {
        using var db = TestComposition.CreateDb();
        var user = SeedUser(db);
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);
        var (setup, _) = await SetupAsync(auth, user, clock);
        var recoveryCode = Assert.Single(setup.RecoveryCodes!.Take(1));

        var firstAttempt = await LoginAsync(auth, user);
        var recovered = await auth.RecoverTwoFactorAsync(new RecoverTwoFactorRequest
        {
            ChallengeId = firstAttempt.ChallengeId,
            RecoveryCode = recoveryCode
        });
        Assert.False(string.IsNullOrWhiteSpace(recovered.Token));

        var secondAttempt = await LoginAsync(auth, user);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            auth.RecoverTwoFactorAsync(new RecoverTwoFactorRequest
            {
                ChallengeId = secondAttempt.ChallengeId,
                RecoveryCode = recoveryCode
            }));
    }

    [Fact]
    public async Task Challenge_CannotBeCompletedWithAnotherUsersCode()
    {
        using var db = TestComposition.CreateDb();
        var firstUser = SeedUser(db, "first@claro.com");
        var secondUser = SeedUser(db, "second@claro.com");
        var clock = new MutableTimeProvider(InitialTime);
        var auth = TestComposition.CreateAuth(db, clock);

        var firstAttempt = await LoginAsync(auth, firstUser);
        var secondAttempt = await LoginAsync(auth, secondUser);
        var secondCode = ComputeTotp(secondAttempt.ManualKey!, clock.GetUtcNow());

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
            {
                ChallengeId = firstAttempt.ChallengeId,
                Code = secondCode
            }));
    }

    [Theory]
    [InlineData(typeof(AdminController), "Admin")]
    [InlineData(typeof(SessionsController), "Agent,Admin")]
    [InlineData(typeof(ChatController), "Customer")]
    [InlineData(typeof(CustomersController), "Customer,Agent,Admin")]
    public void SensitiveControllers_RequireExpectedRoles(Type controllerType, string roles)
    {
        var attribute = Assert.Single(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(roles, attribute.Roles);
    }

    private static async Task<(LoginResponse Response, string Secret)> SetupAsync(
        AuthService auth,
        User user,
        TimeProvider clock)
    {
        var attempt = await LoginAsync(auth, user);
        var secret = attempt.ManualKey!;
        var response = await auth.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
        {
            ChallengeId = attempt.ChallengeId,
            Code = ComputeTotp(secret, clock.GetUtcNow())
        });
        return (response, secret);
    }

    private static Task<LoginAttemptResponse> LoginAsync(AuthService auth, User user)
    {
        return auth.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = Password
        });
    }

    private static string ComputeTotp(string secret, DateTimeOffset now)
    {
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(now.UtcDateTime);
    }

    private static User SeedUser(AppDbContext db, string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Usuário 2FA",
            Email = email ?? $"two-factor-{Guid.NewGuid():N}@claro.com",
            PasswordHash = PasswordProtector.Hash(Password),
            Role = UserRole.Customer,
            CustomerId = DbSeeder.DemoCustomerId,
            CreatedAt = InitialTime.UtcDateTime
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
