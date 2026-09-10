import type { MessageDto } from '../types/api'

export function sameMessageSnapshot(current: MessageDto[], incoming: MessageDto[]) {
  return current.length === incoming.length && current.at(-1)?.id === incoming.at(-1)?.id
}
