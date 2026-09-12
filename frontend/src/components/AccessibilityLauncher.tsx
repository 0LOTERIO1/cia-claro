import { useState } from 'react'
import { AccessibilityPanel } from './AccessibilityPanel'

export function AccessibilityLauncher() {
  const [open, setOpen] = useState(false)

  return (
    <>
      <button
        type="button"
        className="text-btn"
        onClick={() => setOpen(true)}
        aria-haspopup="dialog"
        aria-expanded={open}
      >
        Acessibilidade
      </button>
      <AccessibilityPanel open={open} onClose={() => setOpen(false)} />
    </>
  )
}
