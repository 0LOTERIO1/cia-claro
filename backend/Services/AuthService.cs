using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cia.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITwoFactorRepository _twoFactorRepository;
    private readonly JwtOptions _jwt;
    private readonly TwoFactorOptions _twoFactorOptions;
    private readonly TwoFactorProtector _protector;
    private readonly TwoFactorCodeService _codeService;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserRepository users,
        ITwoFactorRepository twoFactorRepository,
        IOptions<JwtOptions> jwt,
        IOptions<TwoFactorOptions> twoFactorOptions,
        TwoFactorProtector protector,
        TwoFactorCodeService codeService,
        TimeProvider timeProvider)
    {
        _users = users;
        _twoFactorRepository = twoFactorRepository;
        _jwt = jwt.Value;
        _twoFactorOptions = twoFactorOptions.Value;
        _protector = protector;
        _codeService = codeService;
        _timeProvider = timeProvider;
    }

    public async Task<LoginAttemptResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationAppException("E-mail e senha são obrigatórios.");
        }

        var user = await _users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || !PasswordProtector.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("E-mail ou senha inválidos.");
        }

        var now = UtcNow;
        await _twoFactorRepository.InvalidateActiveChallengesAsync(user.Id, now, cancellationToken);

        var requiresSetup = !user.TwoFactorEnabled ||
            string.IsNullOrWhiteSpace(user.TwoFactorSecretEncrypted);
        var secret = requiresSetup ? _codeService.CreateSecret() : null;
        var challenge = new TwoFactorChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = requiresSetup ? TwoFactorChallengePurpose.Setup : TwoFactorChallengePurpose.Login,
            PendingSecretEncrypted = secret is null ? null : _protector.Protect(secret),
            ExpiresAt = now.AddMinutes(Math.Clamp(_twoFactorOptions.ChallengeMinutes, 1, 15)),
            CreatedAt = now
        };

        await _twoFactorRepository.AddChallengeAsync(challenge, cancellationToken);
        await _twoFactorRepository.SaveChangesAsync(cancellationToken);

        return new LoginAttemptResponse
        {
            Status = requiresSetup ? "requiresSetup" : "requiresCode",
            ChallengeId = challenge.Id,
            ExpiresAt = challenge.ExpiresAt,
            ManualKey = secret,
            OtpAuthUri = secret is null ? null : _codeService.CreateOtpAuthUri(user.Email, secret)
        };
    }

    public async Task<LoginResponse> VerifyTwoFactorAsync(
        VerifyTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await GetValidChallengeAsync(request.ChallengeId, cancellationToken);
        var encryptedSecret = challenge.Purpose == TwoFactorChallengePurpose.Setup
            ? challenge.PendingSecretEncrypted
            : challenge.User.TwoFactorSecretEncrypted;

        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new UnauthorizedAppException("Desafio de autenticação inválido ou expirado.");
        }

        var secret = _protector.Unprotect(encryptedSecret);
        if (!_codeService.Verify(secret, request.Code, UtcNow, out var matchedTimeStep) ||
            challenge.User.LastTotpTimeStep >= matchedTimeStep)
        {
            await RegisterFailureAsync(challenge, cancellationToken);
            throw new UnauthorizedAppException("Código de autenticação inválido ou expirado.");
        }

        IReadOnlyList<string>? recoveryCodes = null;
        var now = UtcNow;
        challenge.ConsumedAt = now;
        challenge.User.LastTotpTimeStep = matchedTimeStep;

        if (challenge.Purpose == TwoFactorChallengePurpose.Setup)
        {
            challenge.User.TwoFactorEnabled = true;
            challenge.User.TwoFactorSecretEncrypted = encryptedSecret;
            challenge.User.TwoFactorEnabledAt = now;

            recoveryCodes = CreateRecoveryCodes();
            var entities = recoveryCodes.Select(code => new TwoFactorRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = challenge.UserId,
                CodeHash = _protector.HashRecoveryCode(code),
                CreatedAt = now
            }).ToArray();
            await _twoFactorRepository.ReplaceRecoveryCodesAsync(
                challenge.UserId,
                entities,
                cancellationToken);
        }

        await SaveCompletionAsync(cancellationToken);
        return CreateLoginResponse(challenge.User, recoveryCodes);
    }

    public async Task<LoginResponse> RecoverTwoFactorAsync(
        RecoverTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await GetValidChallengeAsync(request.ChallengeId, cancellationToken);
        if (challenge.Purpose != TwoFactorChallengePurpose.Login ||
            string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            await RegisterFailureAsync(challenge, cancellationToken);
            throw new UnauthorizedAppException("Código de recuperação inválido ou expirado.");
        }

        var hash = _protector.HashRecoveryCode(request.RecoveryCode);
        var recoveryCode = await _twoFactorRepository.GetRecoveryCodeAsync(
            challenge.UserId,
            hash,
            cancellationToken);
        if (recoveryCode is null)
        {
            await RegisterFailureAsync(challenge, cancellationToken);
            throw new UnauthorizedAppException("Código de recuperação inválido ou expirado.");
        }

        var now = UtcNow;
        recoveryCode.UsedAt = now;
        challenge.ConsumedAt = now;
        await SaveCompletionAsync(cancellationToken);
        return CreateLoginResponse(challenge.User);
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        return user.ToDto();
    }

    private async Task<TwoFactorChallenge> GetValidChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var challenge = challengeId == Guid.Empty
            ? null
            : await _twoFactorRepository.GetChallengeAsync(challengeId, cancellationToken);

        if (challenge is null ||
            challenge.ConsumedAt is not null ||
            challenge.ExpiresAt <= UtcNow ||
            challenge.FailedAttempts >= Math.Clamp(_twoFactorOptions.MaxAttempts, 1, 10))
        {
            throw new UnauthorizedAppException("Desafio de autenticação inválido ou expirado.");
        }

        return challenge;
    }

    private async Task RegisterFailureAsync(
        TwoFactorChallenge challenge,
        CancellationToken cancellationToken)
    {
        challenge.FailedAttempts++;
        if (challenge.FailedAttempts >= Math.Clamp(_twoFactorOptions.MaxAttempts, 1, 10))
        {
            challenge.ConsumedAt = UtcNow;
        }

        try
        {
            await _twoFactorRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra tentativa já consumiu ou atualizou o desafio.
        }
    }

    private async Task SaveCompletionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _twoFactorRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedAppException("Desafio de autenticação inválido ou expirado.");
        }
    }

    private IReadOnlyList<string> CreateRecoveryCodes()
    {
        var count = Math.Clamp(_twoFactorOptions.RecoveryCodeCount, 5, 20);
        return Enumerable.Range(0, count)
            .Select(_ => TwoFactorProtector.CreateRecoveryCode())
            .ToArray();
    }

    private LoginResponse CreateLoginResponse(
        User user,
        IReadOnlyList<string>? recoveryCodes = null)
    {
        return new LoginResponse
        {
            Token = CreateToken(user.Id, user.Email, user.Name, user.Role.ToString(), user.CustomerId),
            User = user.ToDto(),
            RecoveryCodes = recoveryCodes
        };
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private string CreateToken(Guid userId, string email, string name, string role, string? customerId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("amr", "mfa")
        };

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            claims.Add(new Claim("customerId", customerId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiresHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
