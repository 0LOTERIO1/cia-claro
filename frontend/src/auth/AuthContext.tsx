import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { clearAuth, persistAuth, getStoredUser } from '../services/api'
import type { UserDto, UserRole } from '../types/api'

interface AuthContextValue {
  user: UserDto | null
  login: (email: string, password: string) => Promise<UserDto>
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
  const [user, setUser] = useState<UserDto | null>(() => getStoredUser())

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      login: async (email: string, password: string) => {
        const response = await apiClient.login(email, password)
        persistAuth(response.token, response.user)
        setUser(response.user)
        return response.user
      },
      logout: () => {
        clearAuth()
        setUser(null)
      },
      homeFor: homeForRole,
    }),
    [user],
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
