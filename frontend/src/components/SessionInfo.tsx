import type { SessionDto } from '../types/api'
import { formatChannel, formatIntent, formatStatus } from '../services/labels'

interface Props {
  session: SessionDto | null
  channelLabel: string
}

export function SessionInfo({ session, channelLabel }: Props) {
  return (
    <section className="panel">
      <h2>Sessão</h2>
      <dl>
        <div>
          <dt>Protocolo</dt>
          <dd className="protocol">{session?.protocol ?? 'Aguardando início'}</dd>
        </div>
        <div>
          <dt>Canal atual</dt>
          <dd>
            <span className={`channel-pill ${channelLabel === 'WhatsApp' ? 'wa' : 'app'}`}>
              {channelLabel.toUpperCase()}
            </span>
          </dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{session ? formatStatus(session.status) : 'Sem atendimento'}</dd>
        </div>
        <div>
          <dt>Intenção</dt>
          <dd>{session ? formatIntent(session.detectedIntent) : '—'}</dd>
        </div>
        {session?.initialChannel && session.initialChannel !== session.currentChannel && (
          <div>
            <dt>Canal inicial</dt>
            <dd>{formatChannel(session.initialChannel)}</dd>
          </div>
        )}
      </dl>
    </section>
  )
}
