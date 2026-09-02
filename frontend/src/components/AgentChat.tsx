import { ChatWindow } from './ChatWindow'
import { HandoffSummary } from './HandoffSummary'
import { JourneyTimeline } from './JourneyTimeline'
import type { AgentSessionDetailDto } from '../types/api'
import { formatDepartment, formatIssue, formatStatus } from '../services/labels'

interface Props {
  detail: AgentSessionDetailDto
  sending: boolean
  onSend: (content: string) => Promise<void>
  onFinish: () => void
}

export function AgentChat({ detail, sending, onSend, onFinish }: Props) {
  const finished = detail.request.status === 'Finished'

  return (
    <div className="layout">
      <aside>
        <section className="panel">
          <h2>Cliente</h2>
          <dl>
            <div>
              <dt>Nome</dt>
              <dd>{detail.customer.name}</dd>
            </div>
            <div>
              <dt>Protocolo</dt>
              <dd className="protocol">{detail.session.protocol}</dd>
            </div>
            <div>
              <dt>Área atual</dt>
              <dd>{formatDepartment(detail.session.currentDepartment)}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd>{formatStatus(detail.session.status)}</dd>
            </div>
            <div>
              <dt>Problema</dt>
              <dd>{detail.request.problem || formatIssue(detail.context?.issueType)}</dd>
            </div>
          </dl>
        </section>
        <section className="panel">
          <h2>Contexto compartilhado</h2>
          <ul className="context-facts">
            {(detail.request.contextFacts.length > 0
              ? detail.request.contextFacts
              : ['Sem fatos adicionais']
            ).map((fact) => (
              <li key={fact}>{fact}</li>
            ))}
          </ul>
        </section>
        <JourneyTimeline current={detail.session.currentDepartment} transfers={detail.transfers} />
        <HandoffSummary handoff={detail.handoff ?? null} />
        {!finished && (
          <button type="button" className="handoff-btn" onClick={onFinish}>
            Encerrar atendimento
          </button>
        )}
      </aside>
      <main>
        <ChatWindow
          messages={detail.messages}
          sending={sending}
          disabled={finished}
          status={detail.session.status}
          selfSender="HumanAgent"
          placeholder={finished ? 'Atendimento encerrado' : 'Escreva para o cliente'}
          onSend={onSend}
        />
      </main>
    </div>
  )
}
