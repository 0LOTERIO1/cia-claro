import { useState, type FormEvent } from 'react'
import { QRCodeSVG } from 'qrcode.react'

interface Props {
  otpAuthUri: string
  manualKey: string
  submitting: boolean
  error: string | null
  onSubmit: (code: string) => Promise<void>
  onBack: () => void
}

export function TwoFactorSetup({
  otpAuthUri,
  manualKey,
  submitting,
  error,
  onSubmit,
  onBack,
}: Props) {
  const [code, setCode] = useState('')
  const normalizedCode = code.replace(/\D/g, '')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    await onSubmit(normalizedCode)
  }

  return (
    <form className="login-form two-factor-setup" onSubmit={(event) => void submit(event)}>
      <h2>Configure o aplicativo autenticador</h2>
      <p className="hint" id="setup-hint">
        Leia o QR code com Google Authenticator, Microsoft Authenticator ou outro aplicativo TOTP.
      </p>
      {error && (
        <div className="banner error" role="alert">
          {error}
        </div>
      )}
      <div className="qr-code" aria-label="QR code para cadastrar o autenticador">
        <QRCodeSVG value={otpAuthUri} size={200} level="M" includeMargin />
      </div>
      <details className="manual-key">
        <summary>Não consegue ler o QR code?</summary>
        <p>Cadastre esta chave manualmente:</p>
        <code>{manualKey}</code>
      </details>
      <label htmlFor="setup-code">
        Código de confirmação
        <input
          id="setup-code"
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          value={code}
          maxLength={8}
          onChange={(event) => setCode(event.target.value)}
          aria-describedby="setup-hint"
          required
        />
      </label>
      <button type="submit" disabled={submitting || normalizedCode.length !== 6}>
        {submitting ? 'Ativando...' : 'Ativar autenticação em dois fatores'}
      </button>
      <button type="button" className="text-btn" onClick={onBack}>
        Voltar ao login
      </button>
    </form>
  )
}
