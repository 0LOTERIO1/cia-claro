using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class AccessibilityPreferences
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public double FontScale { get; set; } = 1.0;
    public bool HighContrast { get; set; }
    public AccessibilityTheme Theme { get; set; } = AccessibilityTheme.System;
    public bool ReducedMotion { get; set; }
    public ReadingSpacing ReadingSpacing { get; set; } = ReadingSpacing.Normal;
    public bool ReadAloudEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
