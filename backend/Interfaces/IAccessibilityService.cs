using Cia.Api.DTOs;
using Cia.Api.Entities;

namespace Cia.Api.Interfaces;

public interface IAccessibilityPreferencesRepository
{
    Task<AccessibilityPreferences?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(AccessibilityPreferences preferences, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAccessibilityService
{
    Task<AccessibilityPreferencesDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccessibilityPreferencesDto> UpdateMineAsync(
        Guid userId,
        AccessibilityPreferencesDto request,
        CancellationToken cancellationToken = default);
}
