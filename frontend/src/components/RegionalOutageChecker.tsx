import { useState, type FormEvent } from 'react'
import { apiClient, getErrorMessage } from '../services/api'
import { formatDateTime } from '../services/labels'
import type { RegionalOutageCheckResponse } from '../types/api'

function formatPostalCode(value: string) {
  const digits = value.replace(/\D/g, '').slice(0, 8)
  return digits.length > 5 ? `${digits.slice(0, 5)}-${digits.slice(5)}` : digits
}

export function RegionalOutageChecker() {
  const [postalCode, setPostalCode] = useState('')
  const [result, setResult] = useState<RegionalOutageCheckResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setChecking(true)
    setError(null)
    setResult(null)
    try {
      setResult(await apiClient.checkRegionalOutage(postalCode))
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setChecking(false)
    }
  }

  return (
    <section className="panel outage-checker">
      <h2>Internet na sua região</h2>
      <p className="hint">Consulte se existe uma indisponibilidade regional cadastrada para o seu CEP.</p>
      <form onSubmit={(event) => void submit(event)}>
        <label htmlFor="outage-postal-code">
          CEP
          <input
            id="outage-postal-code"
            type="text"
            inputMode="numeric"
            autoComplete="postal-code"
            placeholder="00000-000"
            value={postalCode}
            maxLength={9}
            onChange={(event) => setPostalCode(formatPostalCode(event.target.value))}
            required
          />
        </label>
        <button type="submit" className="handoff-btn" disabled={checking || postalCode.replace(/\D/g, '').length !== 8}>
          {checking ? 'Consultando...' : 'Consultar região'}
        </button>
      </form>
      {error && (
        <div className="banner error" role="alert">
          {error}
        </div>
      )}
      {result && (
        <div className={`outage-result ${result.hasOutage ? 'has-outage' : 'is-clear'}`} role="status">
          <strong>{result.hasOutage ? result.outage?.title ?? 'Indisponibilidade identificada' : 'Região sem alerta'}</strong>
          <p>{result.message}</p>
          {result.outage?.description && <p>{result.outage.description}</p>}
          {result.outage?.expectedResolutionAt && (
            <p className="hint">
              Previsão informada: {formatDateTime(result.outage.expectedResolutionAt)}
            </p>
          )}
        </div>
      )}
    </section>
  )
}
