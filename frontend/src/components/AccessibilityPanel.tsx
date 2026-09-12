import { useEffect, useId, useRef } from 'react'
import { useAccessibility } from '../accessibility/AccessibilityContext'
import { FONT_SCALES } from '../accessibility/preferences'
import type { AccessibilityTheme, ReadingSpacing } from '../types/api'

interface Props {
  open: boolean
  onClose: () => void
}

export function AccessibilityPanel({ open, onClose }: Props) {
  const { preferences, saving, update, reset } = useAccessibility()
  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const previously = document.activeElement as HTMLElement | null
    dialogRef.current?.querySelector<HTMLElement>('button, input')?.focus()

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
      }
      if (event.key !== 'Tab' || !dialogRef.current) return
      const focusable = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled]), [href], select, textarea, [tabindex]:not([tabindex="-1"])',
        ),
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previously?.focus()
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="a11y-overlay" onClick={onClose}>
      <div
        ref={dialogRef}
        className="a11y-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="a11y-panel-header">
          <h2 id={titleId}>Acessibilidade</h2>
          <button type="button" className="text-btn" onClick={onClose} aria-label="Fechar painel de acessibilidade">
            Fechar
          </button>
        </header>

        <fieldset>
          <legend>Tamanho do texto</legend>
          <div className="a11y-options" role="group" aria-label="Tamanho do texto">
            {FONT_SCALES.map((scale) => (
              <button
                key={scale}
                type="button"
                className={Math.abs(preferences.fontScale - scale) < 0.001 ? 'is-active' : ''}
                aria-pressed={Math.abs(preferences.fontScale - scale) < 0.001}
                onClick={() => update({ fontScale: scale })}
              >
                {Math.round(scale * 100)}%
              </button>
            ))}
          </div>
        </fieldset>

        <label className="a11y-check">
          <input
            type="checkbox"
            checked={preferences.highContrast}
            onChange={(event) => update({ highContrast: event.target.checked })}
          />
          Alto contraste
        </label>

        <fieldset>
          <legend>Tema</legend>
          <div className="a11y-options" role="radiogroup" aria-label="Tema">
            {(['System', 'Light', 'Dark'] as AccessibilityTheme[]).map((theme) => (
              <label key={theme} className={preferences.theme === theme ? 'is-active' : ''}>
                <input
                  type="radio"
                  name="a11y-theme"
                  checked={preferences.theme === theme}
                  onChange={() => update({ theme })}
                />
                {theme === 'System' ? 'Sistema' : theme === 'Light' ? 'Claro' : 'Escuro'}
              </label>
            ))}
          </div>
        </fieldset>

        <label className="a11y-check">
          <input
            type="checkbox"
            checked={preferences.reducedMotion}
            onChange={(event) => update({ reducedMotion: event.target.checked })}
          />
          Reduzir animações
        </label>

        <fieldset>
          <legend>Espaçamento de leitura</legend>
          <div className="a11y-options" role="radiogroup" aria-label="Espaçamento de leitura">
            {(
              [
                ['Normal', 'Normal'],
                ['Comfortable', 'Confortável'],
                ['Expanded', 'Ampliado'],
              ] as [ReadingSpacing, string][]
            ).map(([value, label]) => (
              <label key={value} className={preferences.readingSpacing === value ? 'is-active' : ''}>
                <input
                  type="radio"
                  name="a11y-spacing"
                  checked={preferences.readingSpacing === value}
                  onChange={() => update({ readingSpacing: value })}
                />
                {label}
              </label>
            ))}
          </div>
        </fieldset>

        <label className="a11y-check">
          <input
            type="checkbox"
            checked={preferences.readAloudEnabled}
            onChange={(event) => update({ readAloudEnabled: event.target.checked })}
          />
          Habilitar “Ouvir mensagem”
        </label>

        <div className="a11y-actions">
          <button type="button" className="text-btn" onClick={reset}>
            Restaurar padrão
          </button>
          <p className="hint" aria-live="polite">
            {saving ? 'Salvando...' : 'Alterações salvas automaticamente.'}
          </p>
        </div>
      </div>
    </div>
  )
}
