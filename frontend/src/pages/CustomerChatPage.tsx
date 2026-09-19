import { useState } from 'react'
import { ChatWindow } from '../components/ChatWindow'
import { HandoffSummary } from '../components/HandoffSummary'
import { JourneyTimeline } from '../components/JourneyTimeline'
import { useAuth } from '../auth/AuthContext'
import { useChat } from '../hooks/useChat'
import { useMediaQuery } from '../hooks/useMediaQuery'
import { formatChannel, formatDateTime, formatDepartment, formatStatus } from '../services/labels'
import { AccessibilityLauncher } from '../components/AccessibilityLauncher'
import { ServiceRatingCard } from '../components/ServiceRatingCard'
import { RegionalOutageChecker } from '../components/RegionalOutageChecker'

export function CustomerChatPage() {
  const { user, logout } = useAuth()
  const chat = useChat(user?.customerId ?? null)
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)
  const waiting = chat.session?.status === 'WaitingForAgent'
  const withAgent = chat.session?.status === 'Transferred'
  const resolved = chat.session?.status === 'Resolved'
  const humanFlow = waiting || withAgent
  const telegramConnected = Boolean(chat.telegram?.connected)
  const pendingTelegramSession =
    Boolean(chat.session) &&
    !chat.resumed &&
    chat.session?.initialChannel === 'Telegram' &&
    chat.session?.currentChannel !== 'WebPortal'
  const desktop = useMediaQuery('(min-width: 1200px)')

  return (
    <div className="app-shell workspace-page theme-app" id="conteudo-principal">
      <header className="topbar">
        <div>
          <p className="eyebrow">Portal do cliente</p>
          <h1>Olá, {user?.name ?? chat.customer?.name ?? 'cliente'}</h1>
        </div>
        <nav className="topbar-actions" aria-label="Ações da conta">
          <AccessibilityLauncher />
          <button type="button" className="text-btn" onClick={logout}>
            Sair
          </button>
        </nav>
      </header>

      {chat.error && (
        <div className="banner error" role="alert">
          {chat.error}
          <button type="button" onClick={() => void chat.reload()}>
            Tentar novamente
          </button>
        </div>
      )}

      {(chat.transferNotice || chat.contextRestored) && (
        <div className="banner info" role="status">
          {chat.transferNotice ?? 'Continuando seu atendimento com o contexto anterior.'}
        </div>
      )}

      {waiting && (
        <div className="banner info" role="status">Aguardando um funcionário da Claro assumir este protocolo.</div>
      )}
      {withAgent && (
        <div className="banner info" role="status">Um atendente assumiu sua conversa. O histórico anterior foi mantido.</div>
      )}

      <div className="customer-workspace">
        <main className="workspace-chat">
          {chat.loading ? (
            <p className="empty">Carregando atendimento...</p>
          ) : pendingTelegramSession ? (
            <section className="panel resume-panel">
              <h2>Continuar no Portal CIA</h2>
              <p>
                Encontramos o atendimento iniciado no Telegram. O protocolo, o histórico e o contexto serão
                mantidos. Nenhuma nova sessão será criada.
              </p>
              <button type="button" className="handoff-btn" onClick={() => void chat.continueAttendance()}>
                Continuar atendimento
              </button>
            </section>
          ) : (
            <>
              {resolved && chat.session && (chat.session.canRate || chat.session.rating) && (
                <ServiceRatingCard
                  canRate={Boolean(chat.session.canRate)}
                  rating={chat.session.rating}
                  submitting={chat.ratingSubmitting}
                  error={chat.ratingError}
                  onSubmit={chat.submitRating}
                />
              )}
              <ChatWindow
                messages={chat.messages}
                sending={chat.sending}
                disabled={Boolean(chat.error && !chat.customer) || resolved}
                status={chat.session?.status}
                onSend={chat.sendMessage}
              />
            </>
          )}
        </main>
        <aside className="workspace-context">
          <details className="context-drawer" {...(desktop ? { open: true } : {})}>
            <summary>Contexto do atendimento</summary>
            <div className="context-stack">
          <RegionalOutageChecker />
          <section className="panel">
            <h2>Canais conectados</h2>
            <div className="channel-status">
              <div>
                <strong>Telegram</strong>
                <p className={telegramConnected ? 'ok' : 'hint'}>
                  {telegramConnected ? 'Telegram conectado' : 'Telegram não conectado'}
                </p>
                {telegramConnected && chat.telegram?.displayName && (
                  <p className="hint">Nome visível: {chat.telegram.displayName}</p>
                )}
              </div>
              {!telegramConnected && !chat.linkCode && (
                <button
                  type="button"
                  className="handoff-btn"
                  disabled={chat.linking}
                  onClick={() => void chat.connectTelegram()}
                >
                  {chat.linking ? 'Gerando código...' : 'Conectar Telegram'}
                </button>
              )}
              {telegramConnected && !confirmDisconnect && (
                <button
                  type="button"
                  className="danger-btn"
                  disabled={chat.unlinking}
                  onClick={() => setConfirmDisconnect(true)}
                >
                  Desconectar Telegram
                </button>
              )}
              {telegramConnected && confirmDisconnect && (
                <div className="confirm-box">
                  <p>
                    Desconectar Telegram? O histórico dos seus atendimentos será preservado, mas este Telegram
                    deixará de estar associado à sua conta CIA.
                  </p>
                  <div className="confirm-actions">
                    <button type="button" className="text-btn" onClick={() => setConfirmDisconnect(false)}>
                      Cancelar
                    </button>
                    <button
                      type="button"
                      className="danger-btn"
                      disabled={chat.unlinking}
                      onClick={() => {
                        void chat.disconnectTelegram().then(() => setConfirmDisconnect(false))
                      }}
                    >
                      {chat.unlinking ? 'Desconectando...' : 'Desconectar'}
                    </button>
                  </div>
                </div>
              )}
              {chat.linkCode && !telegramConnected && (
                <div className="link-box">
                  <p>Abra o bot da CIA no Telegram e envie:</p>
                  <code>{chat.linkCode.command}</code>
                  <p className="hint">Este código expira em {chat.linkCode.expiresInMinutes} minutos.</p>
                </div>
              )}
            </div>
          </section>

          {chat.session && (
            <section className="panel">
              <h2>{resolved ? 'Atendimento finalizado' : 'Atendimento em andamento'}</h2>
              <p className="hint">
                {resolved
                  ? 'Este atendimento foi encerrado. Você pode avaliar a experiência.'
                  : 'Você possui um atendimento em andamento.'}
              </p>
              <dl>
                <div>
                  <dt>Protocolo</dt>
                  <dd className="protocol">{chat.session.protocol}</dd>
                </div>
                <div>
                  <dt>Iniciado via</dt>
                  <dd>{formatChannel(chat.session.initialChannel)}</dd>
                </div>
                <div>
                  <dt>Canal atual</dt>
                  <dd>{formatChannel(chat.session.currentChannel)}</dd>
                </div>
                <div>
                  <dt>Área atual</dt>
                  <dd>{formatDepartment(chat.session.currentDepartment)}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{formatStatus(chat.session.status)}</dd>
                </div>
                <div>
                  <dt>Última atualização</dt>
                  <dd>{formatDateTime(chat.session.updatedAt)}</dd>
                </div>
              </dl>
              {pendingTelegramSession && (
                <button
                  type="button"
                  className="handoff-btn"
                  disabled={chat.sending}
                  onClick={() => void chat.continueAttendance()}
                >
                  Continuar atendimento
                </button>
              )}
            </section>
          )}

          {chat.resumed && (
            <JourneyTimeline current={chat.session?.currentDepartment} transfers={chat.transfers} />
          )}
          {chat.resumed && !humanFlow && chat.session?.status !== 'Resolved' && (
            <button
              type="button"
              className="handoff-btn"
              disabled={!chat.session || chat.sending}
              onClick={() => void chat.requestHandoff()}
            >
              Falar com atendente
            </button>
          )}
          {(resolved || (chat.session?.status === 'Active' && !humanFlow)) && (
            <button
              type="button"
              className="handoff-btn"
              disabled={chat.sending}
              onClick={() => void chat.startNewAttendance()}
            >
              Novo atendimento
            </button>
          )}
          {chat.session?.status === 'Active' && !humanFlow && (
            <button
              type="button"
              className="danger-btn"
              disabled={chat.sending}
              onClick={() => void chat.endAttendance()}
            >
              Encerrar atendimento
            </button>
          )}
          <HandoffSummary handoff={chat.handoff} />
            </div>
          </details>
        </aside>
      </div>
    </div>
  )
}
