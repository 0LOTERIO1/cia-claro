import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AgentChat } from '../components/AgentChat'
import { AgentQueueList } from '../components/AgentQueueList'
import { AccessibilityLauncher } from '../components/AccessibilityLauncher'
import { useAuth } from '../auth/AuthContext'
import { useMediaQuery } from '../hooks/useMediaQuery'
import { apiClient, getErrorMessage } from '../services/api'
import { sameMessageSnapshot } from '../services/messages'
import type { AgentQueueItemDto, AgentSessionDetailDto } from '../types/api'

type AgentTab = 'queue' | 'chat' | 'context'

export function AgentChatPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { logout } = useAuth()
  const splitView = useMediaQuery('(min-width: 1024px)')
  const threeCol = useMediaQuery('(min-width: 1440px)')
  const [tab, setTab] = useState<AgentTab>('chat')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [detail, setDetail] = useState<AgentSessionDetailDto | null>(null)
  const [waiting, setWaiting] = useState<AgentQueueItemDto[]>([])
  const [assigned, setAssigned] = useState<AgentQueueItemDto[]>([])
  const [assumingId, setAssumingId] = useState<string | null>(null)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!id) return
    try {
      const next = await apiClient.getAgentRequest(id)
      setDetail((current) => {
        if (
          current &&
          current.request.status === next.request.status &&
          current.session.status === next.session.status &&
          sameMessageSnapshot(current.messages, next.messages)
        ) {
          return current
        }
        return next
      })
      setError(null)
    } catch (err) {
      setError(getErrorMessage(err))
    }
  }, [id])

  const loadQueue = useCallback(async () => {
    try {
      const [queue, mine] = await Promise.all([apiClient.getAgentQueue(), apiClient.getAgentMine()])
      setWaiting(queue)
      setAssigned(mine)
    } catch {
      // A fila é complementar; o chat continua utilizável se este ciclo falhar.
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    void loadQueue()
  }, [loadQueue])

  useEffect(() => {
    if (!id || detail?.request.status === 'Finished') return
    const timer = window.setInterval(() => void load(), 2500)
    return () => window.clearInterval(timer)
  }, [id, detail?.request.status, load])

  useEffect(() => {
    const timer = window.setInterval(() => void loadQueue(), 3000)
    return () => window.clearInterval(timer)
  }, [loadQueue])

  useEffect(() => {
    setTab('chat')
    setDrawerOpen(false)
  }, [id])

  useEffect(() => {
    if (threeCol) setDrawerOpen(false)
  }, [threeCol])

  const send = async (content: string) => {
    if (!detail) return
    setSending(true)
    try {
      const messages = await apiClient.sendAgentMessage(detail.session.id, content)
      setDetail({ ...detail, messages })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const finish = async () => {
    if (!detail) return
    try {
      setDetail(await apiClient.finishRequest(detail.request.requestId))
      setDrawerOpen(false)
    } catch (err) {
      setError(getErrorMessage(err))
    }
  }

  const assume = async (requestId: string) => {
    setAssumingId(requestId)
    try {
      const next = await apiClient.assumeRequest(requestId)
      navigate(`/agent/${next.request.requestId}`)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAssumingId(null)
    }
  }

  const contextMode = threeCol ? 'column' : splitView ? 'drawer' : 'tab'

  return (
    <div className="app-shell workspace-page theme-app" id="conteudo-principal">
      <header className="topbar">
        <div>
          <p className="eyebrow">Atendimento humano</p>
          <h1>{detail?.session.protocol ?? 'Carregando protocolo...'}</h1>
        </div>
        <nav className="topbar-actions" aria-label="Ações do atendimento">
          {splitView && !threeCol && (
            <button
              type="button"
              className="text-btn"
              aria-haspopup="dialog"
              aria-expanded={drawerOpen}
              onClick={() => setDrawerOpen(true)}
            >
              Abrir contexto da CIA
            </button>
          )}
          <Link to="/agent">Voltar à fila</Link>
          <AccessibilityLauncher />
          <button type="button" className="text-btn" onClick={() => { logout(); navigate('/login') }}>
            Sair
          </button>
        </nav>
      </header>
      {error && <div className="banner error" role="alert">{error}</div>}
      {!splitView && (
        <div className="workspace-tabs" role="tablist" aria-label="Áreas do atendimento">
          <button
            type="button"
            id="tab-queue"
            role="tab"
            aria-selected={tab === 'queue'}
            aria-controls="agent-queue-panel"
            onClick={() => setTab('queue')}
          >
            Fila
          </button>
          <button
            type="button"
            id="tab-chat"
            role="tab"
            aria-selected={tab === 'chat'}
            aria-controls="agent-chat-panel"
            onClick={() => setTab('chat')}
          >
            Chat
          </button>
          <button
            type="button"
            id="tab-context"
            role="tab"
            aria-selected={tab === 'context'}
            aria-controls="agent-context-panel"
            onClick={() => setTab('context')}
          >
            Contexto
          </button>
        </div>
      )}
      {!detail && !error && <p className="empty">Carregando histórico do cliente...</p>}
      {detail && (
        <div className="agent-workspace" data-tab={splitView ? 'all' : tab} data-layout={contextMode}>
          <aside
            className="workspace-queue"
            id="agent-queue-panel"
            role={splitView ? undefined : 'tabpanel'}
            aria-labelledby={splitView ? undefined : 'tab-queue'}
            hidden={!splitView && tab !== 'queue'}
          >
            <AgentQueueList
              waiting={waiting}
              assigned={assigned}
              selectedId={detail.request.requestId}
              assumingId={assumingId}
              compact
              onAssume={(requestId) => void assume(requestId)}
              onOpen={(requestId) => navigate(`/agent/${requestId}`)}
            />
          </aside>
          <AgentChat
            detail={detail}
            sending={sending}
            showChat={splitView || tab === 'chat'}
            showContext={threeCol || (!splitView && tab === 'context')}
            contextMode={contextMode}
            drawerOpen={drawerOpen}
            onCloseDrawer={() => setDrawerOpen(false)}
            onSend={send}
            onFinish={() => void finish()}
          />
        </div>
      )}
    </div>
  )
}
