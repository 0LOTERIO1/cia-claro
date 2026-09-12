import type { DashboardDto } from '../types/api'
import { formatAverageScore, formatDepartment } from '../services/labels'

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

  const distribution = dashboard.scoreDistribution ?? []

  return (
    <>
      <div className="cards">
        {cards.map((card) => (
          <article key={card.label} className="card">
            <span>{card.label}</span>
            <strong>{card.value}</strong>
          </article>
        ))}
      </div>
      <section className="panel rating-metrics" aria-label="Avaliações do atendimento">
        <h2>Avaliação do atendimento</h2>
        <div className="cards">
          <article className="card">
            <span>Nota média</span>
            <strong>{formatAverageScore(dashboard.averageScore)}</strong>
          </article>
          <article className="card">
            <span>Atendimentos avaliados</span>
            <strong>{dashboard.ratedSessions}</strong>
          </article>
          <article className="card">
            <span>Taxa de avaliação</span>
            <strong>{dashboard.ratingRate}%</strong>
          </article>
        </div>
        <h3>Distribuição</h3>
        <ul className="rating-distribution">
          {[...distribution].sort((a, b) => b.score - a.score).map((item) => (
            <li key={item.score}>
              <span>{item.score} ★</span>
              <strong>{item.count}</strong>
            </li>
          ))}
        </ul>
      </section>
    </>
  )
}
