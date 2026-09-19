import { useEffect, useState, type FormEvent } from 'react'
import { apiClient, getErrorMessage } from '../services/api'
import { formatDateTime } from '../services/labels'
import type { RegionalOutageDto } from '../types/api'

export function RegionalOutageManager() {
  const [outages, setOutages] = useState<RegionalOutageDto[]>([])
  const [postalCodePrefix, setPostalCodePrefix] = useState('')
  const [title, setTitle] = useState('Instabilidade de internet na região')
  const [description, setDescription] = useState('')
  const [expectedResolutionAt, setExpectedResolutionAt] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      setOutages(await apiClient.getRegionalOutages())
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void apiClient
      .getRegionalOutages()
      .then((data) => {
        if (active) setOutages(data)
      })
      .catch((err: unknown) => {
        if (active) setError(getErrorMessage(err))
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await apiClient.createRegionalOutage({
        postalCodePrefix,
        title,
        description,
        expectedResolutionAt: expectedResolutionAt
          ? new Date(expectedResolutionAt).toISOString()
          : null,
      })
      setPostalCodePrefix('')
      setDescription('')
      setExpectedResolutionAt('')
      await load()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const resolve = async (id: string) => {
    setSubmitting(true)
    setError(null)
    try {
      await apiClient.resolveRegionalOutage(id)
      await load()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="panel outage-manager">
      <header>
        <div>
          <h2>Indisponibilidades regionais</h2>
          <p className="hint">Cadastre alertas simulados por prefixo de CEP para o protótipo.</p>
        </div>
      </header>
      {error && (
        <div className="banner error" role="alert">
          {error}
        </div>
      )}
      <form className="outage-form" onSubmit={(event) => void submit(event)}>
        <label htmlFor="outage-prefix">
          Prefixo do CEP
          <input
            id="outage-prefix"
            type="text"
            inputMode="numeric"
            placeholder="Ex.: 010 ou 01001"
            value={postalCodePrefix}
            maxLength={8}
            onChange={(event) => setPostalCodePrefix(event.target.value.replace(/\D/g, ''))}
            required
          />
        </label>
        <label htmlFor="outage-title">
          Título
          <input
            id="outage-title"
            type="text"
            value={title}
            maxLength={120}
            onChange={(event) => setTitle(event.target.value)}
            required
          />
        </label>
        <label className="outage-description" htmlFor="outage-description">
          Descrição
          <textarea
            id="outage-description"
            value={description}
            maxLength={500}
            onChange={(event) => setDescription(event.target.value)}
            required
          />
        </label>
        <label htmlFor="outage-resolution">
          Previsão de normalização
          <input
            id="outage-resolution"
            type="datetime-local"
            value={expectedResolutionAt}
            onChange={(event) => setExpectedResolutionAt(event.target.value)}
          />
        </label>
        <button
          type="submit"
          className="handoff-btn"
          disabled={submitting || postalCodePrefix.length < 3 || !title.trim() || !description.trim()}
        >
          {submitting ? 'Salvando...' : 'Cadastrar indisponibilidade'}
        </button>
      </form>
      {loading ? (
        <p className="empty">Carregando indisponibilidades...</p>
      ) : outages.length === 0 ? (
        <p className="empty">Nenhuma indisponibilidade cadastrada.</p>
      ) : (
        <div className="outage-list">
          {outages.map((outage) => (
            <article key={outage.id} className={`outage-card ${outage.active ? 'is-active' : ''}`}>
              <div>
                <span className="status-text">{outage.active ? 'Ativa' : 'Encerrada'}</span>
                <h3>{outage.title}</h3>
                <p>{outage.description}</p>
                <p className="hint">
                  Prefixo {outage.postalCodePrefix} · início {formatDateTime(outage.startedAt)}
                </p>
                {outage.expectedResolutionAt && (
                  <p className="hint">Previsão: {formatDateTime(outage.expectedResolutionAt)}</p>
                )}
              </div>
              {outage.active && (
                <button
                  type="button"
                  className="danger-btn"
                  disabled={submitting}
                  onClick={() => void resolve(outage.id)}
                >
                  Marcar como normalizada
                </button>
              )}
            </article>
          ))}
        </div>
      )}
    </section>
  )
}
