import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiClient, clearAuth, getStoredToken, persistAuth } from '../services/api'
import type { LoginAttemptResponse, LoginResponse, UserDto, UserRole } from '../types/api'

interface AuthContextValue {
  user: UserDto | null
  loading: boolean
  login: (email: string, password: string) => Promise<LoginAttemptResponse>
  verifyTwoFactor: (challengeId: string, code: string) => Promise<LoginResponse>
  recoverTwoFactor: (challengeId: string, recoveryCode: string) => Promise<LoginResponse>
  completeLogin: (response: LoginResponse) => void
  logout: () => void
  homeFor: (role?: UserRole) => string
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function homeForRole(role?: UserRole): string {
  if (role === 'Agent') return '/agent'
  if (role === 'Admin') return '/admin'
  return '/'
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [loading, setLoading] = useState(() => Boolean(getStoredToken()))

  useEffect(() => {
    const token = getStoredToken()
    if (!token) {
      return
    }

    void apiClient
      .me()
      .then((storedUser) => {
        persistAuth(token, storedUser)
        setUser(storedUser)
      })
      .catch(() => {
        clearAuth()
        setUser(null)
      })
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    const handleCleared = () => {
      setUser(null)
      setLoading(false)
    }
    window.addEventListener('cia:auth-cleared', handleCleared)
    return () => window.removeEventListener('cia:auth-cleared', handleCleared)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      login: apiClient.login,
      verifyTwoFactor: apiClient.verifyTwoFactor,
      recoverTwoFactor: apiClient.recoverTwoFactor,
      completeLogin: (response: LoginResponse) => {
        persistAuth(response.token, response.user)
        setUser(response.user)
      },
      logout: () => {
        clearAuth()
        setUser(null)
      },
      homeFor: homeForRole,
    }),
    [loading, user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth deve ser usado dentro de AuthProvider')
  }
  return context
}

