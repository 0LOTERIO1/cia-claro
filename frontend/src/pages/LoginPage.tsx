import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { LoginForm } from '../components/LoginForm'
import { RecoveryCodesPanel } from '../components/RecoveryCodesPanel'
import { TwoFactorForm } from '../components/TwoFactorForm'
import { TwoFactorSetup } from '../components/TwoFactorSetup'
import { AccessibilityLauncher } from '../components/AccessibilityLauncher'
import { getErrorMessage } from '../services/api'
import { homeForRole, useAuth } from '../auth/AuthContext'
import type { LoginAttemptResponse, LoginResponse, UserRole } from '../types/api'

const PROFILES: { role: UserRole; title: string; description: string }[] = [
  { role: 'Customer', title: 'Cliente', description: 'Acompanhe e continue seu atendimento' },
  { role: 'Agent', title: 'Funcionário Claro', description: 'Fila e chat com o cliente' },
  { role: 'Admin', title: 'Admin', description: 'Painel operacional' },
]

export function LoginPage() {
  const {
    user,
    loading,
    login,
    verifyTwoFactor,
    recoverTwoFactor,
    completeLogin,
  } = useAuth()
  const navigate = useNavigate()
  const [profile, setProfile] = useState<UserRole>('Customer')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [step, setStep] = useState<'credentials' | 'setup' | 'code' | 'recovery' | 'recoveryCodes'>(
    'credentials',
  )
  const [attempt, setAttempt] = useState<LoginAttemptResponse | null>(null)
  const [pendingLogin, setPendingLogin] = useState<LoginResponse | null>(null)
  const selected = PROFILES.find((item) => item.role === profile) ?? PROFILES[0]

  if (loading) {
    return (
      <main className="auth-loading" aria-live="polite">
        Validando sessão...
      </main>
    )
  }

  if (user) {
    return <Navigate to={homeForRole(user.role)} replace />
  }

  const handleSubmit = async (email: string, password: string) => {
    setSubmitting(true)
    setError(null)
    try {
      const loginAttempt = await login(email, password)
      setAttempt(loginAttempt)
      setStep(loginAttempt.status === 'requiresSetup' ? 'setup' : 'code')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const finishVerification = (response: LoginResponse) => {
    if (response.recoveryCodes?.length) {
      setPendingLogin(response)
      setStep('recoveryCodes')
      return
    }

    completeLogin(response)
    navigate(homeForRole(response.user.role), { replace: true })
  }

  const handleTotp = async (code: string) => {
    if (!attempt) return
    setSubmitting(true)
    setError(null)
    try {
      finishVerification(await verifyTwoFactor(attempt.challengeId, code))
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const handleRecovery = async (recoveryCode: string) => {
    if (!attempt) return
    setSubmitting(true)
    setError(null)
    try {
      finishVerification(await recoverTwoFactor(attempt.challengeId, recoveryCode))
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  const resetLogin = () => {
    setStep('credentials')
    setAttempt(null)
    setPendingLogin(null)
    setError(null)
  }

  const continueAfterRecoveryCodes = () => {
    if (!pendingLogin) return
    completeLogin(pendingLogin)
    navigate(homeForRole(pendingLogin.user.role), { replace: true })
  }

  return (
    <div className="app-shell login-shell theme-app" id="conteudo-principal">
      <header className="topbar">
        <div>
          <p className="eyebrow">CIA — Claro Inteligência Artificial</p>
          <h1>Entrar na plataforma</h1>
        </div>
        <nav className="topbar-actions" aria-label="Acessibilidade">
          <AccessibilityLauncher />
        </nav>
      </header>
      <section className="panel login-card">
        {step === 'credentials' && (
          <>
            <div className="profile-tabs" role="tablist" aria-label="Tipo de acesso">
              {PROFILES.map((item) => (
                <button
                  key={item.role}
                  type="button"
                  role="tab"
                  aria-selected={item.role === profile}
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
              key={selected.role}
              title={selected.title}
              subtitle={selected.description}
              submitting={submitting}
              error={error}
              onSubmit={handleSubmit}
            />
          </>
        )}
        {step === 'setup' && attempt?.otpAuthUri && attempt.manualKey && (
          <TwoFactorSetup
            otpAuthUri={attempt.otpAuthUri}
            manualKey={attempt.manualKey}
            submitting={submitting}
            error={error}
            onSubmit={handleTotp}
            onBack={resetLogin}
          />
        )}
        {step === 'code' && (
          <TwoFactorForm
            submitting={submitting}
            error={error}
            onSubmit={handleTotp}
            onUseRecovery={() => {
              setStep('recovery')
              setError(null)
            }}
            onBack={resetLogin}
          />
        )}
        {step === 'recovery' && (
          <TwoFactorForm
            recoveryMode
            submitting={submitting}
            error={error}
            onSubmit={handleRecovery}
            onBack={resetLogin}
          />
        )}
        {step === 'recoveryCodes' && pendingLogin?.recoveryCodes && (
          <RecoveryCodesPanel
            codes={pendingLogin.recoveryCodes}
            onContinue={continueAfterRecoveryCodes}
          />
        )}
      </section>
    </div>
  )
}
