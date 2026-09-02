import type { MessageDto, MessageSender } from '../types/api'

interface Props {
  message: MessageDto
  selfSender?: MessageSender
}

function senderLabel(sender: MessageSender, selfSender: MessageSender): string {
  if (sender === selfSender) return 'Você'
  if (sender === 'Customer') return 'Cliente'
  if (sender === 'HumanAgent') return 'Atendente'
  return 'CIA'
}

export function MessageBubble({ message, selfSender = 'Customer' }: Props) {
  const isSelf = message.sender === selfSender
  const tone =
    message.sender === 'HumanAgent' ? 'from-agent' : isSelf ? 'from-customer' : 'from-assistant'

  return (
    <article className={`bubble ${isSelf ? 'from-customer' : tone}`}>
      <header>
        <strong>{senderLabel(message.sender, selfSender)}</strong>
        <time>{new Date(message.createdAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</time>
      </header>
      <p>{message.content}</p>
    </article>
  )
}
