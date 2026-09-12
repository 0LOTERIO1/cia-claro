import type { MessageDto, MessageSender } from '../types/api'
import { formatChannel } from '../services/labels'

interface Props {
  message: MessageDto
  selfSender?: MessageSender
  readAloudEnabled?: boolean
  speechSupported?: boolean
  speaking?: boolean
  onSpeak?: (text: string) => void
  onStopSpeak?: () => void
  includeOwnMessages?: boolean
}

function senderLabel(sender: MessageSender, selfSender: MessageSender): string {
  if (sender === selfSender) return 'Você'
  if (sender === 'Customer') return 'Cliente'
  if (sender === 'HumanAgent') return 'Atendente'
  return 'CIA'
}

function channelClass(channel: string) {
  if (channel === 'Telegram') return 'telegram'
  if (channel === 'WebPortal') return 'portal'
  if (channel === 'WhatsApp') return 'wa'
  return 'app'
}

export function MessageBubble({
  message,
  selfSender = 'Customer',
  readAloudEnabled = false,
  speechSupported = false,
  speaking = false,
  onSpeak,
  onStopSpeak,
  includeOwnMessages = false,
}: Props) {
  const isSelf = message.sender === selfSender
  const tone =
    message.sender === 'HumanAgent' ? 'from-agent' : isSelf ? 'from-customer' : 'from-assistant'
  const canListen =
    readAloudEnabled &&
    speechSupported &&
    Boolean(message.content.trim()) &&
    (includeOwnMessages || !isSelf)

  return (
    <article className={`bubble ${isSelf ? 'from-customer' : tone}`}>
      <header>
        <strong>{senderLabel(message.sender, selfSender)}</strong>
        <span className={`channel-badge ${channelClass(message.channel)}`}>{formatChannel(message.channel)}</span>
        <time dateTime={message.createdAt}>
          {new Date(message.createdAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
        </time>
        {canListen && (
          <button
            type="button"
            className="listen-btn"
            aria-label={speaking ? 'Parar leitura' : 'Ouvir mensagem'}
            onClick={() => (speaking ? onStopSpeak?.() : onSpeak?.(message.content))}
          >
            {speaking ? 'Parar' : 'Ouvir'}
          </button>
        )}
      </header>
      <p>{message.content}</p>
    </article>
  )
}
