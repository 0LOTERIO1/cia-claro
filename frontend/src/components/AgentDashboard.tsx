import type { AgentQueueItemDto } from '../types/api'
import { formatDateTime, formatIssue } from '../services/labels'

interface Props {
  waiting: AgentQueueItemDto[]
  assigned: AgentQueueItemDto[]
  loading: boolean
  assumingId: string | null
  onAssume: (requestId: string) => void
  onOpen: (requestId: string) => void
}

export function AgentDashboard({ waiting, assigned, loading, assumingId, onAssume, onOpen }: Props) {
  if (loading) {
    return <p className="empty">Carregando fila de atendimento...</p>
  }

  return (
    <div className="agent-grid">
      <section className="panel">
        <h2>Aguardando atendente</h2>
        {waiting.length === 0 && <p className="empty">Nenhum cliente na fila no momento.</p>}
        {waiting.map((item) => (
          <article key={item.requestId} className="queue-card">
            <p className="eyebrow">Protocolo</p>
            <h3 className="protocol">{item.protocol}</h3>
            <dl>
              <div>
                <dt>Cliente</dt>
                <dd>{item.customerName}</dd>
              </div>
              <div>
                <dt>Problema</dt>
                <dd>{item.problem || formatIssue()}</dd>
              </div>
            </dl>
            <p className="hint">Contexto</p>
            <ul className="context-facts">
              {(item.contextFacts.length > 0 ? item.contextFacts : ['Histórico ainda em construção']).map((fact) => (
                <li key={fact}>{fact}</li>
              ))}
            </ul>
            <p className="hint">Solicitado em {formatDateTime(item.createdAt)}</p>
            <button
              type="button"
              className="handoff-btn assume-btn"
              disabled={assumingId === item.requestId}
              onClick={() => onAssume(item.requestId)}
            >
              {assumingId === item.requestId ? 'Assumindo...' : 'Assumir atendimento'}
            </button>
          </article>
        ))}
      </section>
      <section className="panel">
        <h2>Meus atendimentos</h2>
        {assigned.length === 0 && <p className="empty">Você ainda não assumiu nenhum protocolo.</p>}
        {assigned.map((item) => (
          <article key={item.requestId} className="queue-card">
            <p className="protocol">{item.protocol}</p>
            <p>
              {item.customerName} — {item.problem}
            </p>
            <button type="button" className="handoff-btn" onClick={() => onOpen(item.requestId)}>
              Abrir chat
            </button>
          </article>
        ))}
      </section>
    </div>
  )
}
