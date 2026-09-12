import { Link } from 'react-router-dom'
import type { SessionDto } from '../types/api'
import { formatChannel, formatDateTime, formatDepartment, formatIntent, formatRatingCell, formatStatus } from '../services/labels'

interface Props {
  sessions: SessionDto[]
}

export function SessionTable({ sessions }: Props) {
  if (sessions.length === 0) {
    return <p className="empty">Nenhum atendimento registrado ainda.</p>
  }

  return (
    <div className="table-wrap" tabIndex={0} aria-label="Tabela de atendimentos">
      <table>
        <thead>
          <tr>
            <th>Protocolo</th>
            <th>Cliente</th>
            <th>Origem</th>
            <th>Canal atual</th>
            <th>Área</th>
            <th>Intenção</th>
            <th>Status</th>
            <th>Avaliação</th>
            <th>Última atualização</th>
          </tr>
        </thead>
        <tbody>
          {sessions.map((session) => (
            <tr key={session.id}>
              <td>
                <Link to={`/admin/sessions/${session.id}`}>{session.protocol}</Link>
              </td>
              <td>{session.customerName}</td>
              <td>{formatChannel(session.initialChannel)}</td>
              <td>{formatChannel(session.currentChannel)}</td>
              <td>{formatDepartment(session.currentDepartment)}</td>
              <td>{formatIntent(session.detectedIntent)}</td>
              <td>
                <span className={`status-text status-${session.status.toLowerCase()}`}>
                  {formatStatus(session.status)}
                </span>
              </td>
              <td>{formatRatingCell(session.rating?.score)}</td>
              <td>{formatDateTime(session.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
