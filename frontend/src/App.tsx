import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AdminDashboardPage } from './pages/AdminDashboardPage'
import { AdminSessionPage } from './pages/AdminSessionPage'
import { AgentChatPage } from './pages/AgentChatPage'
import { AgentDashboardPage } from './pages/AgentDashboardPage'
import { CustomerChatPage } from './pages/CustomerChatPage'
import { LoginPage } from './pages/LoginPage'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <ProtectedRoute roles={['Customer']}>
                <CustomerChatPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/agent"
            element={
              <ProtectedRoute roles={['Agent', 'Admin']}>
                <AgentDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/agent/:id"
            element={
              <ProtectedRoute roles={['Agent', 'Admin']}>
                <AgentChatPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute roles={['Admin']}>
                <AdminDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/sessions/:id"
            element={
              <ProtectedRoute roles={['Admin']}>
                <AdminSessionPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
