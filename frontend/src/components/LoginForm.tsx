import { useState, type FormEvent } from 'react'

interface Props {
  title: string
  subtitle: string
  submitting: boolean
  error: string | null
  onSubmit: (email: string, password: string) => Promise<void>
}

export function LoginForm({ title, subtitle, submitting, error, onSubmit }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    await onSubmit(email.trim(), password)
  }

  return (
    <form className="login-form" onSubmit={(event) => void submit(event)} autoComplete="off">
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
    </form>
  )
}
