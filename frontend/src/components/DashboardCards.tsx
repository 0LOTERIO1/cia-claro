import type { DashboardDto } from '../types/api'
import { formatAverageScore, formatDepartment } from '../services/labels'

interface Props {
  dashboard: DashboardDto
}

export function DashboardCards({ dashboard }: Props) {
  const summary = [
    { label: 'Total de atendimentos', value: dashboard.totalSessions },
    { label: 'Ativos', value: dashboard.activeSessions },
    { label: 'Resolvidos', value: dashboard.resolvedSessions },
    { label: 'Transferidos', value: dashboard.transferredSessions },
    { label: 'Nota média', value: formatAverageScore(dashboard.averageScore) },
    { label: 'Atendimentos avaliados', value: dashboard.ratedSessions },
    { label: 'Taxa de avaliação', value: `${dashboard.ratingRate}%` },
  ]
  const areas = dashboard.sessionsByDepartment ?? []
  const distribution = dashboard.scoreDistribution ?? []
  const maxCount = Math.max(1, ...distribution.map((item) => item.count))

  return (
    <>
      <section className="panel admin-section" aria-label="Resumo operacional">
        <h2>Resumo</h2>
        <div className="cards admin-kpis">
          {summary.map((card) => (
            <article key={card.label} className="card">
              <span>{card.label}</span>
              <strong>{card.value}</strong>
            </article>
          ))}
        </div>
        <h3>Distribuição de notas</h3>
        <ul className="rating-distribution">
          {[...distribution].sort((a, b) => b.score - a.score).map((item) => (
            <li key={item.score}>
              <span>{item.score} ★</span>
              <div className="bar-track" aria-hidden="true">
                <span
                  className="bar-fill"
                  style={{ width: `${Math.round((item.count / maxCount) * 100)}%` }}
                />
              </div>
              <strong>{item.count}</strong>
            </li>
          ))}
        </ul>
      </section>
      {areas.length > 0 && (
        <section className="panel admin-section" aria-label="Atendimentos por área">
          <h2>Atendimentos por área</h2>
          <div className="cards admin-areas">
            {areas.map((item) => (
              <article key={item.department} className="card">
                <span>{formatDepartment(item.department)}</span>
                <strong>{item.count}</strong>
              </article>
            ))}
          </div>
        </section>
      )}
    </>
  )
}
