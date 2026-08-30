import type { HandoffDto } from '../types/api'

interface Props {
  handoff: HandoffDto | null
}

export function HandoffSummary({ handoff }: Props) {
  if (!handoff) return null

  return (
    <section className="panel handoff">
      <h2>Resumo do transbordo</h2>
      <pre>{handoff.summary}</pre>
    </section>
  )
}
