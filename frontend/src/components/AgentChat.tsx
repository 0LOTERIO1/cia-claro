import { useEffect, useId, useRef } from 'react'
import { createPortal } from 'react-dom'
import { ChatWindow } from './ChatWindow'
import { HandoffSummary } from './HandoffSummary'
import { JourneyTimeline } from './JourneyTimeline'
import type { AgentSessionDetailDto } from '../types/api'
import { formatChannel, formatDepartment, formatIssue, formatStatus } from '../services/labels'
import { ServiceRatingCard } from './ServiceRatingCard'

export type AgentContextMode = 'column' | 'drawer' | 'tab'

interface Props {
  detail: AgentSessionDetailDto
  sending: boolean
  onSend: (content: string) => Promise<void>
  onFinish: () => void
  showChat?: boolean
  showContext?: boolean
  contextMode?: AgentContextMode
  drawerOpen?: boolean
  onCloseDrawer?: () => void
}

function AgentContextBody({ detail, onFinish }: { detail: AgentSessionDetailDto; onFinish: () => void }) {
  const finished = detail.request.status === 'Finished'

  return (
    <div className="context-stack">
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
        </dl>
      </section>
      <section className="panel">
        <h2>Atendimento</h2>
        <dl>
          <div>
            <dt>Canal atual</dt>
            <dd>{formatChannel(detail.session.currentChannel)}</dd>
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
      {(detail.agentBriefing || detail.responseSuggestion) && (
        <section className="panel">
          <h2>Resumo da CIA</h2>
          {detail.agentBriefing && <p>{detail.agentBriefing}</p>}
          {detail.responseSuggestion && (
            <p>
              <strong>Sugestão (não enviada):</strong> {detail.responseSuggestion}
            </p>
          )}
        </section>
      )}
      <section className="panel">
        <h2>Fatos conhecidos</h2>
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
        <button type="button" className="handoff-btn" onClick={onFinish} aria-label="Encerrar atendimento">
          Encerrar atendimento
        </button>
      )}
      {finished && (
        <p className="hint" role="status">
          {detail.session.rating
            ? `Cliente avaliou: ${detail.session.rating.score}/5`
            : 'Atendimento finalizado. Aguardando avaliação do cliente.'}
        </p>
      )}
      {finished && detail.session.rating && (
        <ServiceRatingCard readOnly rating={detail.session.rating} />
      )}
    </div>
  )
}

export function AgentChat({
  detail,
  sending,
  onSend,
  onFinish,
  showChat = true,
  showContext = true,
  contextMode = 'column',
  drawerOpen = false,
  onCloseDrawer,
}: Props) {
  const finished = detail.request.status === 'Finished'
  const tabbed = contextMode === 'tab'
  const titleId = useId()
  const drawerRef = useRef<HTMLElement>(null)

  useEffect(() => {
    if (contextMode !== 'drawer' || !drawerOpen) return
    const previously = document.activeElement as HTMLElement | null
    drawerRef.current?.querySelector<HTMLElement>('button, [href], input')?.focus()

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onCloseDrawer?.()
        return
      }
      if (event.key !== 'Tab' || !drawerRef.current) return
      const focusable = Array.from(
        drawerRef.current.querySelectorAll<HTMLElement>(
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
  }, [contextMode, drawerOpen, onCloseDrawer])

  return (
    <>
      <main
        className="workspace-chat"
        id="agent-chat-panel"
        hidden={!showChat}
        role={tabbed ? 'tabpanel' : undefined}
        aria-labelledby={tabbed ? 'tab-chat' : undefined}
      >
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
      {contextMode === 'drawer' && drawerOpen && createPortal(
        <div className="context-drawer-overlay" onClick={() => onCloseDrawer?.()}>
          <aside
            ref={drawerRef}
            className="context-drawer-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            onClick={(event) => event.stopPropagation()}
          >
            <header className="context-drawer-header">
              <h2 id={titleId}>Contexto da CIA</h2>
              <button type="button" className="text-btn" onClick={() => onCloseDrawer?.()}>
                Fechar
              </button>
            </header>
            <AgentContextBody detail={detail} onFinish={onFinish} />
          </aside>
        </div>,
        document.body,
      )}
      {contextMode !== 'drawer' && (
        <aside
          className="workspace-context agent-context"
          id="agent-context-panel"
          hidden={!showContext}
          role={tabbed ? 'tabpanel' : undefined}
          aria-labelledby={tabbed ? 'tab-context' : undefined}
        >
          <AgentContextBody detail={detail} onFinish={onFinish} />
        </aside>
      )}
    </>
  )
}
