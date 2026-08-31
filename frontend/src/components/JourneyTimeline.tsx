import type { DepartmentType, TransferDto } from '../types/api'
import { formatDepartment } from '../services/labels'

interface Props {
  current?: DepartmentType | null
  transfers: TransferDto[]
}

const ORDER: DepartmentType[] = [
  'Triage',
  'TechnicalSupport',
  'ModemReplacement',
  'Financial',
  'HumanAgent',
]

export function JourneyTimeline({ current, transfers }: Props) {
  const visited = new Set<DepartmentType>(['Triage'])
  transfers.forEach((item) => visited.add(item.toDepartment))
  if (current) visited.add(current)

  const steps = ORDER.filter((item) => visited.has(item))
  const active = current ?? 'Triage'

  return (
    <section className="panel">
      <h2>Jornada do atendimento</h2>
      <ol className="journey">
        {steps.map((step) => (
          <li key={step} className={step === active ? 'is-current' : 'is-done'}>
            <span>{step === active ? '→' : '✓'}</span>
            {formatDepartment(step)}
          </li>
        ))}
      </ol>
    </section>
  )
}
