import type { DashboardDto } from '../types/api'
import { formatDepartment } from '../services/labels'

interface Props {
  dashboard: DashboardDto
}

export function DashboardCards({ dashboard }: Props) {
  const cards = [
    { label: 'Total de atendimentos', value: dashboard.totalSessions },
    { label: 'Ativos', value: dashboard.activeSessions },
    { label: 'Resolvidos', value: dashboard.resolvedSessions },
    { label: 'Transferidos', value: dashboard.transferredSessions },
    ...(dashboard.sessionsByDepartment ?? []).map((item) => ({
      label: formatDepartment(item.department),
      value: item.count,
    })),
  ]

  return (
    <div className="cards">
      {cards.map((card) => (
        <article key={card.label} className="card">
          <span>{card.label}</span>
          <strong>{card.value}</strong>
        </article>
      ))}
    </div>
  )
}
