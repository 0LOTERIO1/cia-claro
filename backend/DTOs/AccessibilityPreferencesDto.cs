using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class AccessibilityPreferencesDto
{
    public double FontScale { get; set; } = 1.0;
    public bool HighContrast { get; set; }
    public AccessibilityTheme Theme { get; set; } = AccessibilityTheme.System;
    public bool ReducedMotion { get; set; }
    public ReadingSpacing ReadingSpacing { get; set; } = ReadingSpacing.Normal;
    public bool ReadAloudEnabled { get; set; }
}
