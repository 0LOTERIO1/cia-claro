import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { useAuth } from '../auth/AuthContext'
import { apiClient } from '../services/api'
import type { AccessibilityPreferencesDto } from '../types/api'
import {
  applyAccessibilityToDocument,
  DEFAULT_ACCESSIBILITY_PREFERENCES,
  normalizeAccessibilityPreferences,
  readCachedPreferences,
  writeCachedPreferences,
} from './preferences'

interface AccessibilityContextValue {
  preferences: AccessibilityPreferencesDto
  saving: boolean
  update: (patch: Partial<AccessibilityPreferencesDto>) => void
  reset: () => void
}

const AccessibilityContext = createContext<AccessibilityContextValue | null>(null)

export function AccessibilityProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userId = user?.id ?? null
  const [preferences, setPreferences] = useState<AccessibilityPreferencesDto>(() => {
    const cached = readCachedPreferences(userId) ?? readCachedPreferences()
    return cached ?? DEFAULT_ACCESSIBILITY_PREFERENCES
  })
  const [saving, setSaving] = useState(false)
  const skipPersist = useRef(true)
  const debounceRef = useRef<number | undefined>(undefined)

  useEffect(() => {
    applyAccessibilityToDocument(preferences)
  }, [preferences])

  useEffect(() => {
    skipPersist.current = true
    const cached = readCachedPreferences(userId)
    if (cached) {
      setPreferences(cached)
      applyAccessibilityToDocument(cached)
    } else if (!userId) {
      setPreferences(DEFAULT_ACCESSIBILITY_PREFERENCES)
      applyAccessibilityToDocument(DEFAULT_ACCESSIBILITY_PREFERENCES)
    }

    if (!userId) {
      skipPersist.current = false
      return
    }

    let cancelled = false
    void apiClient
      .getAccessibilityPreferences()
      .then((remote) => {
        if (cancelled) return
        const next = normalizeAccessibilityPreferences(remote)
        setPreferences(next)
        writeCachedPreferences(next, userId)
        applyAccessibilityToDocument(next)
      })
      .catch(() => {
        /* keep cache */
      })
      .finally(() => {
        skipPersist.current = false
      })

    return () => {
      cancelled = true
    }
  }, [userId])

  const persist = useCallback(
    (next: AccessibilityPreferencesDto) => {
      writeCachedPreferences(next, userId)
      applyAccessibilityToDocument(next)
      if (!userId) return
      window.clearTimeout(debounceRef.current)
      debounceRef.current = window.setTimeout(() => {
        setSaving(true)
        void apiClient
          .saveAccessibilityPreferences(next)
          .catch(() => undefined)
          .finally(() => setSaving(false))
      }, 400)
    },
    [userId],
  )

  const update = useCallback(
    (patch: Partial<AccessibilityPreferencesDto>) => {
      setPreferences((current) => {
        const next = normalizeAccessibilityPreferences({ ...current, ...patch })
        if (!skipPersist.current) persist(next)
        return next
      })
    },
    [persist],
  )

  const reset = useCallback(() => {
    setPreferences(DEFAULT_ACCESSIBILITY_PREFERENCES)
    persist(DEFAULT_ACCESSIBILITY_PREFERENCES)
  }, [persist])

  const value = useMemo(
    () => ({ preferences, saving, update, reset }),
    [preferences, saving, update, reset],
  )

  return <AccessibilityContext.Provider value={value}>{children}</AccessibilityContext.Provider>
}

export function useAccessibility() {
  const context = useContext(AccessibilityContext)
  if (!context) {
    throw new Error('useAccessibility deve ser usado dentro de AccessibilityProvider')
  }
  return context
}
