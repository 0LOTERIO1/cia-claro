import { Link } from 'react-router-dom'
import { ChannelSelector } from '../components/ChannelSelector'
import { ChatWindow } from '../components/ChatWindow'
import { CustomerInfo } from '../components/CustomerInfo'
import { HandoffSummary } from '../components/HandoffSummary'
import { SessionInfo } from '../components/SessionInfo'
import { useChat } from '../hooks/useChat'
import { formatChannel } from '../services/labels'

export function CustomerChatPage() {
  const chat = useChat()
  const theme = chat.channel === 'WhatsApp' ? 'theme-whatsapp' : 'theme-app'

  return (
    <div className={`app-shell ${theme}`}>
      <header className="topbar">
        <div>
          <p className="eyebrow">Atendimento omnicanal</p>
          <h1>CIA — Claro Inteligência Artificial</h1>
        </div>
        <nav>
          <Link to="/admin">Painel administrativo</Link>
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

      {chat.contextRestored && (
        <div className="banner info">Contexto do atendimento anterior recuperado.</div>
      )}

      <div className="layout">
        <aside>
          <CustomerInfo customer={chat.customer} />
          <SessionInfo session={chat.session} channelLabel={formatChannel(chat.channel)} />
          <section className="panel">
            <h2>Trocar canal</h2>
            <ChannelSelector
              channel={chat.channel}
              disabled={chat.sending || chat.loading}
              onChange={(channel) => void chat.changeChannel(channel)}
            />
            <p className="hint">
              A troca atualiza o canal da mesma sessão. O protocolo permanece o mesmo.
            </p>
          </section>
          <button
            type="button"
            className="handoff-btn"
            disabled={!chat.session || chat.sending || chat.session.status === 'Transferred'}
            onClick={() => void chat.requestHandoff()}
          >
            Falar com atendente
          </button>
          {chat.session?.status === 'Transferred' && (
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
              disabled={Boolean(chat.error && !chat.customer)}
              status={chat.session?.status}
              onSend={chat.sendMessage}
            />
          )}
        </main>
      </div>
    </div>
  )
}
