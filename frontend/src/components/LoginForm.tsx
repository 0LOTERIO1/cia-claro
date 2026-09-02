import { useState, type FormEvent } from 'react'
import type { UserRole } from '../types/api'

interface Props {
  title: string
  subtitle: string
  submitting: boolean
  error: string | null
  onSubmit: (email: string, password: string) => Promise<void>
  demoEmail: string
  demoRole: UserRole
}

export function LoginForm({ title, subtitle, submitting, error, onSubmit, demoEmail, demoRole }: Props) {
  const [email, setEmail] = useState(demoEmail)
  const [password, setPassword] = useState('Claro@123')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    await onSubmit(email.trim(), password)
  }

  return (
    <form className="login-form" onSubmit={(event) => void submit(event)}>
      <h2>{title}</h2>
      <p className="hint">{subtitle}</p>
      {error && <div className="banner error">{error}</div>}
      <label>
        E-mail
        <input
          type="email"
          value={email}
          autoComplete="username"
          onChange={(event) => setEmail(event.target.value)}
          required
        />
      </label>
      <label>
        Senha
        <input
          type="password"
          value={password}
          autoComplete="current-password"
          onChange={(event) => setPassword(event.target.value)}
          required
        />
      </label>
      <button type="submit" disabled={submitting || !email.trim() || !password}>
        {submitting ? 'Entrando...' : 'Entrar'}
      </button>
      <p className="hint">
        Demonstração {demoRole === 'Customer' ? 'cliente' : demoRole === 'Agent' ? 'funcionário' : 'admin'}:{' '}
        <strong>{demoEmail}</strong> / <strong>Claro@123</strong>
      </p>
    </form>
  )
}
