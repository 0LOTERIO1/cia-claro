import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { LoginForm } from '../components/LoginForm'
import { getErrorMessage } from '../services/api'
import { homeForRole, useAuth } from '../auth/AuthContext'
import type { UserRole } from '../types/api'

const PROFILES: { role: UserRole; title: string; email: string; description: string }[] = [
  { role: 'Customer', title: 'Cliente', email: 'lucas@claro.com', description: 'Chat com a CIA e atendimento humano' },
  { role: 'Agent', title: 'Funcionário Claro', email: 'agente@claro.com', description: 'Fila e chat com o cliente' },
  { role: 'Admin', title: 'Admin', email: 'admin@claro.com', description: 'Painel operacional' },
]

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const [profile, setProfile] = useState<UserRole>('Customer')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const selected = PROFILES.find((item) => item.role === profile) ?? PROFILES[0]

  if (user) {
    return <Navigate to={homeForRole(user.role)} replace />
  }

  const handleSubmit = async (email: string, password: string) => {
    setSubmitting(true)
    setError(null)
    try {
      const logged = await login(email, password)
      navigate(homeForRole(logged.role), { replace: true })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="app-shell theme-app">
      <header className="topbar">
        <div>
          <p className="eyebrow">CIA — Claro Inteligência Artificial</p>
          <h1>Entrar na plataforma</h1>
        </div>
      </header>
      <section className="panel login-card">
        <div className="profile-tabs">
          {PROFILES.map((item) => (
            <button
              key={item.role}
              type="button"
              className={item.role === profile ? 'is-active' : ''}
              onClick={() => {
                setProfile(item.role)
                setError(null)
              }}
            >
              {item.title}
            </button>
          ))}
        </div>
        <LoginForm
          key={selected.email}
          title={selected.title}
          subtitle={selected.description}
          submitting={submitting}
          error={error}
          onSubmit={handleSubmit}
          demoEmail={selected.email}
          demoRole={selected.role}
        />
      </section>
    </div>
  )
}
