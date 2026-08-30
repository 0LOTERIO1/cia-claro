import type { MessageDto } from '../types/api'
import { formatChannel } from '../services/labels'

interface Props {
  message: MessageDto
}

export function MessageBubble({ message }: Props) {
  const isCustomer = message.sender === 'Customer'
  const label =
    message.sender === 'Customer'
      ? 'Você'
      : message.sender === 'HumanAgent'
        ? 'Atendente'
        : 'CIA'

  return (
    <article className={`bubble ${isCustomer ? 'from-customer' : 'from-assistant'}`}>
      <header>
        <strong>{label}</strong>
        <span>{formatChannel(message.channel)}</span>
        <time>{new Date(message.createdAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</time>
      </header>
      <p>{message.content}</p>
    </article>
  )
}
