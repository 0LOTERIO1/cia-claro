import type { AccessibilityPreferencesDto, AccessibilityTheme, ReadingSpacing } from '../types/api'

export const FONT_SCALES = [1, 1.15, 1.3, 1.5] as const
export type FontScale = (typeof FONT_SCALES)[number]

export const DEFAULT_ACCESSIBILITY_PREFERENCES: AccessibilityPreferencesDto = {
  fontScale: 1,
  highContrast: false,
  theme: 'System',
  reducedMotion: false,
  readingSpacing: 'Normal',
  readAloudEnabled: false,
}

const THEMES: AccessibilityTheme[] = ['System', 'Light', 'Dark']
const SPACINGS: ReadingSpacing[] = ['Normal', 'Comfortable', 'Expanded']

export function isAllowedFontScale(value: number): value is FontScale {
  return FONT_SCALES.some((scale) => Math.abs(scale - value) < 0.001)
}

export function normalizeAccessibilityPreferences(
  input?: Partial<AccessibilityPreferencesDto> | null,
): AccessibilityPreferencesDto {
  const fontScale = isAllowedFontScale(input?.fontScale ?? 1) ? input!.fontScale! : 1
  const theme = THEMES.includes(input?.theme as AccessibilityTheme) ? (input!.theme as AccessibilityTheme) : 'System'
  const readingSpacing = SPACINGS.includes(input?.readingSpacing as ReadingSpacing)
    ? (input!.readingSpacing as ReadingSpacing)
    : 'Normal'

  return {
    fontScale,
    highContrast: Boolean(input?.highContrast),
    theme,
    reducedMotion: Boolean(input?.reducedMotion),
    readingSpacing,
    readAloudEnabled: Boolean(input?.readAloudEnabled),
  }
}

export function accessibilityCacheKey(userId?: string | null): string {
  return userId ? `cia.a11y.${userId}` : 'cia.a11y.anon'
}

export function readCachedPreferences(userId?: string | null): AccessibilityPreferencesDto | null {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(accessibilityCacheKey(userId))
    if (!raw) return null
    return normalizeAccessibilityPreferences(JSON.parse(raw) as Partial<AccessibilityPreferencesDto>)
  } catch {
    return null
  }
}

export function writeCachedPreferences(preferences: AccessibilityPreferencesDto, userId?: string | null) {
  if (typeof localStorage === 'undefined') return
  localStorage.setItem(accessibilityCacheKey(userId), JSON.stringify(normalizeAccessibilityPreferences(preferences)))
}

export function applyAccessibilityToDocument(preferences: AccessibilityPreferencesDto) {
  if (typeof document === 'undefined') return
  const prefs = normalizeAccessibilityPreferences(preferences)
  const root = document.documentElement
  root.style.setProperty('--font-scale', String(prefs.fontScale))
  root.dataset.theme = prefs.theme.toLowerCase()
  root.dataset.highContrast = prefs.highContrast ? 'true' : 'false'
  root.dataset.reducedMotion = prefs.reducedMotion ? 'true' : 'false'
  root.dataset.readingSpacing = prefs.readingSpacing.toLowerCase()
  root.classList.toggle('a11y-high-contrast', prefs.highContrast)
}

export function prefersReducedMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return false
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export function shouldReduceMotion(preferences: AccessibilityPreferencesDto): boolean {
  return preferences.reducedMotion || prefersReducedMotion()
}
