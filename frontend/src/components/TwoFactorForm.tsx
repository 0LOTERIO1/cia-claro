import { useState, type FormEvent } from 'react'

interface Props {
  recoveryMode?: boolean
  submitting: boolean
  error: string | null
  onSubmit: (code: string) => Promise<void>
  onUseRecovery?: () => void
  onBack: () => void
}

export function TwoFactorForm({
  recoveryMode = false,
  submitting,
  error,
  onSubmit,
  onUseRecovery,
  onBack,
}: Props) {
  const [code, setCode] = useState('')
  const normalized = recoveryMode ? code.trim() : code.replace(/\D/g, '')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    await onSubmit(normalized)
  }

  return (
    <form className="login-form two-factor-form" onSubmit={(event) => void submit(event)}>
      <h2>{recoveryMode ? 'Usar código de recuperação' : 'Confirme sua identidade'}</h2>
      <p className="hint" id="two-factor-hint">
        {recoveryMode
          ? 'Digite um dos códigos de recuperação salvos durante o cadastro.'
          : 'Digite o código de 6 dígitos exibido no seu aplicativo autenticador.'}
      </p>
      {error && (
        <div className="banner error" role="alert">
          {error}
        </div>
      )}
      <label htmlFor="two-factor-code">
        {recoveryMode ? 'Código de recuperação' : 'Código do autenticador'}
        <input
          id="two-factor-code"
          type="text"
          inputMode={recoveryMode ? 'text' : 'numeric'}
          autoComplete={recoveryMode ? 'off' : 'one-time-code'}
          value={code}
          maxLength={recoveryMode ? 32 : 8}
          onChange={(event) => setCode(event.target.value)}
          aria-describedby="two-factor-hint"
          autoFocus
          required
        />
      </label>
      <button type="submit" disabled={submitting || (recoveryMode ? normalized.length < 10 : normalized.length !== 6)}>
        {submitting ? 'Verificando...' : 'Verificar e entrar'}
      </button>
      <div className="auth-secondary-actions">
        {!recoveryMode && onUseRecovery && (
          <button type="button" className="text-btn" onClick={onUseRecovery}>
            Usar código de recuperação
          </button>
        )}
        <button type="button" className="text-btn" onClick={onBack}>
          Voltar ao login
        </button>
      </div>
    </form>
  )
}
