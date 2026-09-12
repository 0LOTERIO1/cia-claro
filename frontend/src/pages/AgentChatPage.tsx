import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AgentChat } from '../components/AgentChat'
import { AccessibilityLauncher } from '../components/AccessibilityLauncher'
import { useAuth } from '../auth/AuthContext'
import { apiClient, getErrorMessage } from '../services/api'
import { sameMessageSnapshot } from '../services/messages'
import type { AgentSessionDetailDto } from '../types/api'

export function AgentChatPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { logout } = useAuth()
  const [detail, setDetail] = useState<AgentSessionDetailDto | null>(null)
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

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!id || detail?.request.status === 'Finished') return
    const timer = window.setInterval(() => void load(), 2500)
    return () => window.clearInterval(timer)
  }, [id, detail?.request.status, load])

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
    } catch (err) {
      setError(getErrorMessage(err))
    }
  }

  return (
    <div className="app-shell theme-app" id="conteudo-principal">
      <header className="topbar">
        <div>
          <p className="eyebrow">Atendimento humano</p>
          <h1>{detail?.session.protocol ?? 'Carregando protocolo...'}</h1>
        </div>
        <nav className="topbar-actions" aria-label="Ações do atendimento">
          <Link to="/agent">Voltar à fila</Link>
          <AccessibilityLauncher />
          <button type="button" className="text-btn" onClick={() => { logout(); navigate('/login') }}>
            Sair
          </button>
        </nav>
      </header>
      {error && <div className="banner error" role="alert">{error}</div>}
      {!detail && !error && <p className="empty">Carregando histórico do cliente...</p>}
      {detail && (
        <AgentChat
          detail={detail}
          sending={sending}
          onSend={send}
          onFinish={() => void finish()}
        />
      )}
    </div>
  )
}
