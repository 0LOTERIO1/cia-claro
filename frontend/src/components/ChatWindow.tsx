import { useEffect, useRef, useState, type FormEvent, type UIEvent } from 'react'
import { useAccessibility } from '../accessibility/AccessibilityContext'
import { shouldReduceMotion } from '../accessibility/preferences'
import { useSpeechSynthesis } from '../accessibility/useSpeechSynthesis'
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

function announceLabel(sender: MessageSender, selfSender: MessageSender) {
  if (sender === selfSender) return 'Você'
  if (sender === 'Customer') return 'Cliente'
  if (sender === 'HumanAgent') return 'Atendente'
  return 'CIA'
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
  const [liveMessage, setLiveMessage] = useState('')
  const historyRef = useRef<HTMLDivElement>(null)
  const nearBottomRef = useRef(true)
  const lastMessageIdRef = useRef<string | undefined>(undefined)
  const lastAnnouncedIdRef = useRef<string | undefined>(undefined)
  const pendingOwnSendRef = useRef(false)
  const locked = disabled || status === 'Resolved'
  const { preferences } = useAccessibility()
  const speech = useSpeechSynthesis()
  const [speakingId, setSpeakingId] = useState<string | null>(null)

  const scrollHistoryToBottom = (smooth = true) => {
    const element = historyRef.current
    if (!element) return
    const reduceMotion = shouldReduceMotion(preferences)
    element.scrollTo({
      top: element.scrollHeight,
      behavior: smooth && !reduceMotion ? 'smooth' : 'auto',
    })
    nearBottomRef.current = true
  }

  useEffect(() => {
    const last = messages.at(-1)
    const lastId = last?.id
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

    if (last && last.sender !== selfSender && last.id !== lastAnnouncedIdRef.current) {
      lastAnnouncedIdRef.current = last.id
      setLiveMessage(`${announceLabel(last.sender, selfSender)}: ${last.content}`)
    }
  }, [messages, selfSender, preferences])

  useEffect(() => {
    if (!speech.speaking) setSpeakingId(null)
  }, [speech.speaking])

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
      <p className="sr-only" aria-live="polite" aria-atomic="true">
        {liveMessage}
      </p>
      <div
        ref={historyRef}
        className="chat-history"
        role="region"
        aria-label="Histórico da conversa"
        onScroll={handleHistoryScroll}
      >
        {messages.length === 0 && (
          <p className="empty">Envie uma mensagem para iniciar o atendimento com a CIA.</p>
        )}
        {messages.map((message) => (
          <MessageBubble
            key={message.id}
            message={message}
            selfSender={selfSender}
            readAloudEnabled={preferences.readAloudEnabled}
            speechSupported={speech.supported}
            speaking={speakingId === message.id}
            onSpeak={(content) => {
              setSpeakingId(message.id)
              speech.speak(content)
            }}
            onStopSpeak={() => {
              speech.stop()
              setSpeakingId(null)
            }}
          />
        ))}
        {sending && !isHumanChat(status) && selfSender === 'Customer' && (
          <div className="typing" role="status">
            CIA está processando...
          </div>
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
          aria-describedby="message-hint"
        />
        <span id="message-hint" className="sr-only">
          Pressione Enter para enviar
        </span>
        <button type="submit" disabled={locked || sending || !text.trim()} aria-label="Enviar mensagem">
          {sending ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </section>
  )
}
