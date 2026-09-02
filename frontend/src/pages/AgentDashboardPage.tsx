import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AgentDashboard } from '../components/AgentDashboard'
import { useAuth } from '../auth/AuthContext'
import { apiClient, getErrorMessage } from '../services/api'
import type { AgentQueueItemDto } from '../types/api'

export function AgentDashboardPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [waiting, setWaiting] = useState<AgentQueueItemDto[]>([])
  const [assigned, setAssigned] = useState<AgentQueueItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [assumingId, setAssumingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      const [queue, mine] = await Promise.all([apiClient.getAgentQueue(), apiClient.getAgentMine()])
      setWaiting(queue)
      setAssigned(mine)
      setError(null)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
    const timer = window.setInterval(() => void load(), 3000)
    return () => window.clearInterval(timer)
  }, [load])

  const assume = async (requestId: string) => {
    setAssumingId(requestId)
    try {
      const detail = await apiClient.assumeRequest(requestId)
      navigate(`/agent/${detail.request.requestId}`)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setAssumingId(null)
    }
  }

  return (
    <div className="app-shell theme-app">
      <header className="topbar">
        <div>
          <p className="eyebrow">Funcionário Claro</p>
          <h1>Fila de atendimento humano</h1>
          <p className="hint">Olá, {user?.name}. Assuma um protocolo para continuar o histórico do cliente.</p>
        </div>
        <nav className="topbar-actions">
          {user?.role === 'Admin' && <Link to="/admin">Admin</Link>}
          <button type="button" className="text-btn" onClick={logout}>
            Sair
          </button>
        </nav>
      </header>
      {error && <div className="banner error">{error}</div>}
      <AgentDashboard
        waiting={waiting}
        assigned={assigned}
        loading={loading}
        assumingId={assumingId}
        onAssume={(id) => void assume(id)}
        onOpen={(id) => navigate(`/agent/${id}`)}
      />
    </div>
  )
}
