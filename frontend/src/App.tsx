import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AdminDashboardPage } from './pages/AdminDashboardPage'
import { AdminSessionPage } from './pages/AdminSessionPage'
import { CustomerChatPage } from './pages/CustomerChatPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<CustomerChatPage />} />
        <Route path="/admin" element={<AdminDashboardPage />} />
        <Route path="/admin/sessions/:id" element={<AdminSessionPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
