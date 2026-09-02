import { useEffect, useRef, useState, type FormEvent } from 'react'
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
  const endRef = useRef<HTMLDivElement>(null)
  const locked = disabled || status === 'Resolved'

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, sending])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const value = text.trim()
    if (!value || locked) return
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
      <div className="chat-history" role="log" aria-live="polite">
        {messages.length === 0 && (
          <p className="empty">Envie uma mensagem para iniciar o atendimento com a CIA.</p>
        )}
        {messages.map((message) => (
          <MessageBubble key={message.id} message={message} selfSender={selfSender} />
        ))}
        {sending && !isHumanChat(status) && selfSender === 'Customer' && (
          <div className="typing">CIA está processando...</div>
        )}
        <div ref={endRef} />
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
