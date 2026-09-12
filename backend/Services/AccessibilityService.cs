using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class AccessibilityService : IAccessibilityService
{
    public static readonly double[] AllowedFontScales = { 1.0, 1.15, 1.30, 1.50 };

    private readonly IUserRepository _users;
    private readonly IAccessibilityPreferencesRepository _preferences;
    private readonly ILogger<AccessibilityService> _logger;

    public AccessibilityService(
        IUserRepository users,
        IAccessibilityPreferencesRepository preferences,
        ILogger<AccessibilityService> logger)
    {
        _users = users;
        _preferences = preferences;
        _logger = logger;
    }

    public async Task<AccessibilityPreferencesDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateAsync(userId, cancellationToken);
        return ToDto(entity);
    }

    public async Task<AccessibilityPreferencesDto> UpdateMineAsync(
        Guid userId,
        AccessibilityPreferencesDto request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var entity = await GetOrCreateAsync(userId, cancellationToken);
        entity.FontScale = NormalizeScale(request.FontScale);
        entity.HighContrast = request.HighContrast;
        entity.Theme = request.Theme;
        entity.ReducedMotion = request.ReducedMotion;
        entity.ReadingSpacing = request.ReadingSpacing;
        entity.ReadAloudEnabled = request.ReadAloudEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _preferences.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Accessibility preferences updated. UserId={UserId}", userId);
        return ToDto(entity);
    }

    private async Task<AccessibilityPreferences> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _preferences.GetByUserIdAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        _ = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        var created = Defaults(userId);
        await _preferences.AddAsync(created, cancellationToken);
        await _preferences.SaveChangesAsync(cancellationToken);
        return created;
    }

    public static AccessibilityPreferences Defaults(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FontScale = 1.0,
        HighContrast = false,
        Theme = AccessibilityTheme.System,
        ReducedMotion = false,
        ReadingSpacing = ReadingSpacing.Normal,
        ReadAloudEnabled = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static void Validate(AccessibilityPreferencesDto request)
    {
        if (!Enum.IsDefined(request.Theme))
        {
            throw new ValidationAppException("Tema inválido. Use System, Light ou Dark.");
        }

        if (!Enum.IsDefined(request.ReadingSpacing))
        {
            throw new ValidationAppException("Espaçamento inválido. Use Normal, Comfortable ou Expanded.");
        }

        if (!AllowedFontScales.Any(scale => Math.Abs(scale - request.FontScale) < 0.001))
        {
            throw new ValidationAppException("Tamanho de texto inválido. Use 100%, 115%, 130% ou 150%.");
        }
    }

    private static double NormalizeScale(double scale) =>
        AllowedFontScales.OrderBy(value => Math.Abs(value - scale)).First();

    private static AccessibilityPreferencesDto ToDto(AccessibilityPreferences entity) => new()
    {
        FontScale = entity.FontScale,
        HighContrast = entity.HighContrast,
        Theme = entity.Theme,
        ReducedMotion = entity.ReducedMotion,
        ReadingSpacing = entity.ReadingSpacing,
        ReadAloudEnabled = entity.ReadAloudEnabled
    };
}
