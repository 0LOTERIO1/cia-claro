import { Navigate } from 'react-router-dom'
import { homeForRole, useAuth } from './AuthContext'
import type { UserRole } from '../types/api'

interface Props {
  roles: UserRole[]
  children: React.ReactNode
}

export function ProtectedRoute({ roles, children }: Props) {
  const { user } = useAuth()

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (!roles.includes(user.role)) {
    return <Navigate to={homeForRole(user.role)} replace />
  }

  return children
}
