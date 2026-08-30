import { useNavigate } from 'react-router-dom'
import type { SessionDto } from '../types/api'
import { formatChannel, formatDateTime, formatIntent, formatStatus } from '../services/labels'

interface Props {
  sessions: SessionDto[]
}

export function SessionTable({ sessions }: Props) {
  const navigate = useNavigate()

  if (sessions.length === 0) {
    return <p className="empty">Nenhum atendimento registrado ainda.</p>
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Protocolo</th>
            <th>Cliente</th>
            <th>Canal</th>
            <th>Intenção</th>
            <th>Status</th>
            <th>Última atualização</th>
          </tr>
        </thead>
        <tbody>
          {sessions.map((session) => (
            <tr key={session.id} onClick={() => navigate(`/admin/sessions/${session.id}`)}>
              <td>{session.protocol}</td>
              <td>{session.customerName}</td>
              <td>{formatChannel(session.currentChannel)}</td>
              <td>{formatIntent(session.detectedIntent)}</td>
              <td>{formatStatus(session.status)}</td>
              <td>{formatDateTime(session.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
