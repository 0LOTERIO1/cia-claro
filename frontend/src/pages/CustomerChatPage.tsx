import { Link } from 'react-router-dom'
import { ChatWindow } from '../components/ChatWindow'
import { CustomerInfo } from '../components/CustomerInfo'
import { HandoffSummary } from '../components/HandoffSummary'
import { JourneyTimeline } from '../components/JourneyTimeline'
import { SessionInfo } from '../components/SessionInfo'
import { useAuth } from '../auth/AuthContext'
import { useChat } from '../hooks/useChat'
import type { DepartmentType } from '../types/api'

const MANUAL_ROUTES: { department: DepartmentType; label: string; reason: string }[] = [
  { department: 'TechnicalSupport', label: 'Encaminhar para Técnico', reason: 'Transferência manual para Suporte Técnico' },
  { department: 'ModemReplacement', label: 'Encaminhar para Troca de Modem', reason: 'Transferência manual para Troca de Modem' },
  { department: 'Financial', label: 'Encaminhar para Financeiro', reason: 'Transferência manual para Financeiro' },
]

export function CustomerChatPage() {
  const { user, logout } = useAuth()
  const chat = useChat(user?.customerId ?? null)
  const waiting = chat.session?.status === 'WaitingForAgent'
  const withAgent = chat.session?.status === 'Transferred'
  const humanFlow = waiting || withAgent

  return (
    <div className="app-shell theme-app">
      <header className="topbar">
        <div>
          <p className="eyebrow">Contexto compartilhado entre áreas</p>
          <h1>CIA — Claro Inteligência Artificial</h1>
        </div>
        <nav className="topbar-actions">
          {user?.role === 'Admin' && <Link to="/admin">Painel administrativo</Link>}
          <button type="button" className="text-btn" onClick={logout}>
            Sair
          </button>
        </nav>
      </header>

      {chat.error && (
        <div className="banner error">
          {chat.error}
          <button type="button" onClick={() => void chat.reload()}>
            Tentar novamente
          </button>
        </div>
      )}

      {(chat.transferNotice || chat.contextRestored) && (
        <div className="banner info">
          {chat.transferNotice ?? 'Continuando seu atendimento com o contexto anterior.'}
        </div>
      )}

      {waiting && (
        <div className="banner info">Aguardando um funcionário da Claro assumir este protocolo.</div>
      )}
      {withAgent && (
        <div className="banner info">Um atendente assumiu sua conversa. O histórico anterior foi mantido.</div>
      )}

      <div className="layout">
        <aside>
          <CustomerInfo customer={chat.customer} />
          <SessionInfo session={chat.session} />
          <JourneyTimeline current={chat.session?.currentDepartment} transfers={chat.transfers} />
          <section className="panel">
            <h2>Demonstração</h2>
            <p className="hint">O roteamento ocorre pelas mensagens. Use os botões só se precisar forçar uma área.</p>
            <div className="demo-actions">
              {MANUAL_ROUTES.map((item) => (
                <button
                  key={item.department}
                  type="button"
                  className="handoff-btn"
                  disabled={!chat.session || chat.sending || humanFlow}
                  onClick={() => void chat.changeDepartment(item.department, item.reason)}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </section>
          <button
            type="button"
            className="handoff-btn"
            disabled={!chat.session || chat.sending || humanFlow}
            onClick={() => void chat.requestHandoff()}
          >
            Falar com atendente
          </button>
          {chat.session?.status === 'Resolved' && (
            <button type="button" className="handoff-btn" onClick={chat.startNewAttendance}>
              Novo atendimento
            </button>
          )}
          <HandoffSummary handoff={chat.handoff} />
        </aside>
        <main>
          {chat.loading ? (
            <p className="empty">Carregando atendimento...</p>
          ) : (
            <ChatWindow
              messages={chat.messages}
              sending={chat.sending}
              disabled={Boolean(chat.error && !chat.customer) || chat.session?.status === 'Resolved'}
              status={chat.session?.status}
              onSend={chat.sendMessage}
            />
          )}
        </main>
      </div>
    </div>
  )
}
