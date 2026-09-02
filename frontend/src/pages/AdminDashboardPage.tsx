import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { DashboardCards } from '../components/DashboardCards'
import { SessionTable } from '../components/SessionTable'
import { useAuth } from '../auth/AuthContext'
import { apiClient, getErrorMessage } from '../services/api'
import type { DashboardDto, SessionDto } from '../types/api'

export function AdminDashboardPage() {
  const { logout } = useAuth()
  const [dashboard, setDashboard] = useState<DashboardDto | null>(null)
  const [sessions, setSessions] = useState<SessionDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const [dashboardData, sessionData] = await Promise.all([
          apiClient.getDashboard(),
          apiClient.getAdminSessions(),
        ])
        setDashboard(dashboardData)
        setSessions(sessionData)
      } catch (err) {
        setError(getErrorMessage(err))
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  return (
    <div className="app-shell theme-app">
      <header className="topbar">
        <div>
          <p className="eyebrow">Operação</p>
          <h1>Dashboard administrativo</h1>
        </div>
        <nav className="topbar-actions">
          <Link to="/agent">Fila humana</Link>
          <button type="button" className="text-btn" onClick={logout}>
            Sair
          </button>
        </nav>
      </header>
      {error && <div className="banner error">{error}</div>}
      {loading && <p className="empty">Carregando indicadores...</p>}
      {dashboard && <DashboardCards dashboard={dashboard} />}
      <section className="panel">
        <h2>Atendimentos</h2>
        <SessionTable sessions={sessions} />
      </section>
    </div>
  )
}
