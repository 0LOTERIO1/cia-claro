import { AgentQueueList } from './AgentQueueList'
import type { AgentQueueItemDto } from '../types/api'

interface Props {
  waiting: AgentQueueItemDto[]
  assigned: AgentQueueItemDto[]
  loading: boolean
  assumingId: string | null
  onAssume: (requestId: string) => void
  onOpen: (requestId: string) => void
}

export function AgentDashboard({ waiting, assigned, loading, assumingId, onAssume, onOpen }: Props) {
  if (loading) {
    return <p className="empty">Carregando fila de atendimento...</p>
  }

  return (
    <AgentQueueList
      waiting={waiting}
      assigned={assigned}
      assumingId={assumingId}
      compact
      onAssume={onAssume}
      onOpen={onOpen}
    />
  )
}
