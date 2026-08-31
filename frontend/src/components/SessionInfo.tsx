import type { SessionDto } from '../types/api'
import { formatDepartment, formatIntent, formatStatus } from '../services/labels'

interface Props {
  session: SessionDto | null
}

export function SessionInfo({ session }: Props) {
  return (
    <section className="panel">
      <h2>Sessão</h2>
      <dl>
        <div>
          <dt>Protocolo</dt>
          <dd className="protocol">{session?.protocol ?? 'Aguardando início'}</dd>
        </div>
        <div>
          <dt>Área atual</dt>
          <dd>
            <span className="channel-pill app">{formatDepartment(session?.currentDepartment).toUpperCase()}</span>
          </dd>
        </div>
        {session?.previousDepartment && (
          <div>
            <dt>Área anterior</dt>
            <dd>{formatDepartment(session.previousDepartment)}</dd>
          </div>
        )}
        <div>
          <dt>Status</dt>
          <dd>{session ? formatStatus(session.status) : 'Sem atendimento'}</dd>
        </div>
        <div>
          <dt>Intenção</dt>
          <dd>{session ? formatIntent(session.detectedIntent) : '—'}</dd>
        </div>
      </dl>
    </section>
  )
}
