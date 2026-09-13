import type { AgentQueueItemDto } from '../types/api'
import { formatDateTime, formatIssue } from '../services/labels'

interface Props {
  waiting: AgentQueueItemDto[]
  assigned: AgentQueueItemDto[]
  selectedId?: string
  assumingId: string | null
  compact?: boolean
  onAssume: (requestId: string) => void
  onOpen: (requestId: string) => void
}

export function AgentQueueList({
  waiting,
  assigned,
  selectedId,
  assumingId,
  compact = false,
  onAssume,
  onOpen,
}: Props) {
  return (
    <div className={compact ? 'queue-list' : 'queue-board'}>
      <section className="panel queue-panel" aria-label="Aguardando atendente">
        <h2>Aguardando atendente</h2>
        {waiting.length === 0 && <p className="empty">Nenhum cliente na fila no momento.</p>}
        <div className="queue-scroll">
          {waiting.map((item) => (
            <article
              key={item.requestId}
              className={`queue-card${compact ? ' is-compact' : ''}${selectedId === item.requestId ? ' is-selected' : ''}`}
            >
              <p className="eyebrow">Aguardando</p>
              <h3 className="protocol">{item.protocol}</h3>
              <p className="queue-name">{item.customerName}</p>
              {!compact && (
                <>
                  <p className="hint">{item.problem || formatIssue()}</p>
                  <p className="hint">Solicitado em {formatDateTime(item.createdAt)}</p>
                  <ul className="context-facts">
                    {(item.contextFacts.length > 0 ? item.contextFacts : ['Histórico ainda em construção']).map((fact) => (
                      <li key={fact}>{fact}</li>
                    ))}
                  </ul>
                </>
              )}
              <button
                type="button"
                className="handoff-btn assume-btn"
                disabled={assumingId === item.requestId}
                onClick={() => onAssume(item.requestId)}
                aria-label={`Assumir atendimento do protocolo ${item.protocol}`}
                aria-current={selectedId === item.requestId ? 'true' : undefined}
              >
                {assumingId === item.requestId ? 'Assumindo...' : compact ? 'Assumir' : 'Assumir atendimento'}
              </button>
            </article>
          ))}
        </div>
      </section>
      <section className="panel queue-panel" aria-label="Meus atendimentos">
        <h2>Meus atendimentos</h2>
        {assigned.length === 0 && <p className="empty">Você ainda não assumiu nenhum protocolo.</p>}
        <div className="queue-scroll">
          {assigned.map((item) => (
            <article
              key={item.requestId}
              className={`queue-card${compact ? ' is-compact' : ''}${selectedId === item.requestId ? ' is-selected' : ''}`}
            >
              <p className="eyebrow">Em atendimento</p>
              <h3 className="protocol">{item.protocol}</h3>
              <p className="queue-name">{item.customerName}</p>
              {!compact && item.problem && <p className="hint">{item.problem}</p>}
              <button
                type="button"
                className="handoff-btn"
                onClick={() => onOpen(item.requestId)}
                aria-current={selectedId === item.requestId ? 'page' : undefined}
              >
                {selectedId === item.requestId
                  ? compact ? 'Aberto' : 'Atendimento aberto'
                  : compact ? 'Abrir' : 'Abrir chat'}
              </button>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
