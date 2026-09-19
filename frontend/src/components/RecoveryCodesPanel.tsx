import { useState } from 'react'

interface Props {
  codes: string[]
  onContinue: () => void
}

export function RecoveryCodesPanel({ codes, onContinue }: Props) {
  const [copied, setCopied] = useState(false)
  const [copyError, setCopyError] = useState(false)
  const [confirmed, setConfirmed] = useState(false)

  const copyCodes = async () => {
    try {
      await navigator.clipboard.writeText(codes.join('\n'))
      setCopied(true)
      setCopyError(false)
    } catch {
      setCopyError(true)
    }
  }

  return (
    <section className="login-form recovery-codes" aria-labelledby="recovery-codes-title">
      <h2 id="recovery-codes-title">Salve seus códigos de recuperação</h2>
      <div className="banner info" role="status">
        Cada código funciona uma única vez. Guarde-os fora deste dispositivo.
      </div>
      <ul aria-label="Códigos de recuperação">
        {codes.map((code) => (
          <li key={code}>
            <code>{code}</code>
          </li>
        ))}
      </ul>
      <button type="button" className="secondary-btn" onClick={() => void copyCodes()}>
        {copied ? 'Códigos copiados' : 'Copiar códigos'}
      </button>
      {copyError && (
        <p className="hint" role="alert">
          Não foi possível copiar automaticamente. Selecione e copie os códigos manualmente.
        </p>
      )}
      <label className="recovery-confirmation">
        <input
          type="checkbox"
          checked={confirmed}
          onChange={(event) => setConfirmed(event.target.checked)}
        />
        Confirmo que salvei os códigos em um local seguro
      </label>
      <button type="button" disabled={!confirmed} onClick={onContinue}>
        Continuar para a plataforma
      </button>
    </section>
  )
}
