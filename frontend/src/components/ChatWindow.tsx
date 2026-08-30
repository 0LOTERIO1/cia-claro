import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { MessageDto, SessionStatus } from '../types/api'
import { MessageBubble } from './MessageBubble'

interface Props {
  messages: MessageDto[]
  sending: boolean
  disabled: boolean
  status?: SessionStatus
  onSend: (content: string) => Promise<void>
}

export function ChatWindow({ messages, sending, disabled, status, onSend }: Props) {
  const [text, setText] = useState('')
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, sending])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const value = text.trim()
    if (!value) return
    setText('')
    await onSend(value)
  }

  return (
    <section className="chat-window">
      <div className="chat-history" role="log" aria-live="polite">
        {messages.length === 0 && (
          <p className="empty">Envie uma mensagem para iniciar o atendimento com a CIA.</p>
        )}
        {messages.map((message) => (
          <MessageBubble key={message.id} message={message} />
        ))}
        {sending && <div className="typing">CIA está processando...</div>}
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
          placeholder={status === 'Transferred' ? 'Atendimento transferido' : 'Digite sua mensagem'}
          disabled={disabled || sending || status === 'Transferred'}
        />
        <button type="submit" disabled={disabled || sending || !text.trim() || status === 'Transferred'}>
          {sending ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </section>
  )
}
