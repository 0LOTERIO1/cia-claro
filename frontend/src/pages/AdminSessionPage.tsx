import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { HandoffSummary } from '../components/HandoffSummary'
import { JourneyTimeline } from '../components/JourneyTimeline'
import { MessageBubble } from '../components/MessageBubble'
import { apiClient, getErrorMessage } from '../services/api'
import {
  formatDateTime,
  formatDepartment,
  formatIntent,
  formatIssue,
  formatStatus,
} from '../services/labels'
import type { AdminSessionDetailDto } from '../types/api'

export function AdminSessionPage() {
  const { id } = useParams()
  const [detail, setDetail] = useState<AdminSessionDetailDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    const load = async () => {
      try {
        setDetail(await apiClient.getAdminSession(id))
      } catch (err) {
        setError(getErrorMessage(err))
      }
    }
    void load()
  }, [id])

  return (
    <div className="app-shell theme-app">
      <header className="topbar">
        <div>
          <p className="eyebrow">Detalhe do atendimento</p>
          <h1>{detail?.session.protocol ?? 'Carregando...'}</h1>
        </div>
        <nav>
          <Link to="/admin">Voltar ao dashboard</Link>
        </nav>
      </header>
      {error && <div className="banner error">{error}</div>}
      {detail && (
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
                  <dt>Customer ID</dt>
                  <dd>{detail.customer.id}</dd>
                </div>
                <div>
                  <dt>Telefone</dt>
                  <dd>{detail.customer.phone}</dd>
                </div>
              </dl>
            </section>
            <section className="panel">
              <h2>Sessão</h2>
              <dl>
                <div>
                  <dt>Protocolo</dt>
                  <dd className="protocol">{detail.session.protocol}</dd>
                </div>
                <div>
                  <dt>Área atual</dt>
                  <dd>{formatDepartment(detail.session.currentDepartment)}</dd>
                </div>
                <div>
                  <dt>Área anterior</dt>
                  <dd>{formatDepartment(detail.session.previousDepartment)}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{formatStatus(detail.session.status)}</dd>
                </div>
                <div>
                  <dt>Intenção</dt>
                  <dd>{formatIntent(detail.session.detectedIntent)}</dd>
                </div>
                <div>
                  <dt>Atualizado</dt>
                  <dd>{formatDateTime(detail.session.updatedAt)}</dd>
                </div>
              </dl>
            </section>
            <section className="panel">
              <h2>Contexto</h2>
              <dl>
                <div>
                  <dt>Problema original</dt>
                  <dd>{detail.context?.originalProblem ?? formatIssue(detail.context?.issueType)}</dd>
                </div>
                <div>
                  <dt>Modem reiniciado</dt>
                  <dd>{detail.context?.modemRestarted ? 'Sim' : 'Não'}</dd>
                </div>
                <div>
                  <dt>Problema persistiu</dt>
                  <dd>{detail.context?.internetStillDown ? 'Sim' : 'Não'}</dd>
                </div>
                <div>
                  <dt>Pedido atual</dt>
                  <dd>{detail.context?.currentRequest ?? '—'}</dd>
                </div>
                <div>
                  <dt>Resumo</dt>
                  <dd>{detail.context?.contextSummary ?? '—'}</dd>
                </div>
              </dl>
            </section>
            <JourneyTimeline
              current={detail.session.currentDepartment}
              transfers={detail.transfers ?? detail.session.transfers ?? []}
            />
            <HandoffSummary handoff={detail.handoff ?? null} />
          </aside>
          <main className="chat-window">
            <div className="chat-history">
              {detail.messages.map((message) => (
                <MessageBubble key={message.id} message={message} />
              ))}
            </div>
          </main>
        </div>
      )}
    </div>
  )
}
