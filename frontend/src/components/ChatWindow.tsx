import { useEffect, useRef, useState, type FormEvent, type UIEvent } from 'react'
import type { MessageDto, MessageSender, SessionStatus } from '../types/api'
import { MessageBubble } from './MessageBubble'

interface Props {
  messages: MessageDto[]
  sending: boolean
  disabled: boolean
  status?: SessionStatus
  selfSender?: MessageSender
  placeholder?: string
  onSend: (content: string) => Promise<void>
}

function isHumanChat(status?: SessionStatus) {
  return status === 'WaitingForAgent' || status === 'Transferred'
}

const NEAR_BOTTOM_PX = 100

function isNearBottom(element: HTMLElement) {
  const distanceFromBottom = element.scrollHeight - element.scrollTop - element.clientHeight
  return distanceFromBottom < NEAR_BOTTOM_PX
}

export function ChatWindow({
  messages,
  sending,
  disabled,
  status,
  selfSender = 'Customer',
  placeholder,
  onSend,
}: Props) {
  const [text, setText] = useState('')
  const historyRef = useRef<HTMLDivElement>(null)
  const nearBottomRef = useRef(true)
  const lastMessageIdRef = useRef<string | undefined>(undefined)
  const pendingOwnSendRef = useRef(false)
  const locked = disabled || status === 'Resolved'

  const scrollHistoryToBottom = (smooth = true) => {
    const element = historyRef.current
    if (!element) return
    element.scrollTo({
      top: element.scrollHeight,
      behavior: smooth ? 'smooth' : 'auto',
    })
    nearBottomRef.current = true
  }

  useEffect(() => {
    const lastId = messages.at(-1)?.id
    const hasNewMessage = lastId !== lastMessageIdRef.current
    const ownSend = pendingOwnSendRef.current

    if (ownSend) {
      pendingOwnSendRef.current = false
      lastMessageIdRef.current = lastId
      scrollHistoryToBottom()
      return
    }

    if (!hasNewMessage) {
      return
    }

    lastMessageIdRef.current = lastId

    const element = historyRef.current
    if (element && isNearBottom(element)) {
      nearBottomRef.current = true
    }

    if (nearBottomRef.current) {
      scrollHistoryToBottom()
    }
  }, [messages])

  const handleHistoryScroll = (event: UIEvent<HTMLDivElement>) => {
    nearBottomRef.current = isNearBottom(event.currentTarget)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const value = text.trim()
    if (!value || locked) return
    pendingOwnSendRef.current = true
    setText('')
    await onSend(value)
  }

  const inputPlaceholder =
    placeholder ??
    (status === 'WaitingForAgent'
      ? 'Aguardando atendente. Você pode continuar escrevendo.'
      : status === 'Transferred'
        ? 'Converse com o atendente'
        : status === 'Resolved'
          ? 'Atendimento encerrado'
          : 'Digite sua mensagem')

  return (
    <section className="chat-window">
      <div
        ref={historyRef}
        className="chat-history"
        role="log"
        aria-live="polite"
        onScroll={handleHistoryScroll}
      >
        {messages.length === 0 && (
          <p className="empty">Envie uma mensagem para iniciar o atendimento com a CIA.</p>
        )}
        {messages.map((message) => (
          <MessageBubble key={message.id} message={message} selfSender={selfSender} />
        ))}
        {sending && !isHumanChat(status) && selfSender === 'Customer' && (
          <div className="typing">CIA está processando...</div>
        )}
      </div>
      <form className="composer" onSubmit={(event) => void submit(event)}>
        <label className="sr-only" htmlFor="message">
          Mensagem
        </label>
        <input
          id="message"
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder={inputPlaceholder}
          disabled={locked || sending}
        />
        <button type="submit" disabled={locked || sending || !text.trim()}>
          {sending ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </section>
  )
}
