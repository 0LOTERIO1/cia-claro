import { useCallback, useEffect, useState } from 'react'
import { apiClient, getErrorMessage } from '../services/api'
import { DEMO_CUSTOMER_ID } from '../services/labels'
import type {
  ChannelType,
  CustomerDto,
  HandoffDto,
  MessageDto,
  SessionDto,
} from '../types/api'

export function useChat() {
  const [customer, setCustomer] = useState<CustomerDto | null>(null)
  const [session, setSession] = useState<SessionDto | null>(null)
  const [messages, setMessages] = useState<MessageDto[]>([])
  const [handoff, setHandoff] = useState<HandoffDto | null>(null)
  const [channel, setChannel] = useState<ChannelType>('AppClaro')
  const [contextRestored, setContextRestored] = useState(false)
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const customerData = await apiClient.getCustomer(DEMO_CUSTOMER_ID)
      setCustomer(customerData)

      const sessions = await apiClient.getSessionsByCustomer(DEMO_CUSTOMER_ID)
      const current = sessions.find((item) => item.status === 'Active') ?? sessions[0] ?? null
      setSession(current)

      if (current) {
        setChannel(current.currentChannel)
        const history = await apiClient.getMessages(current.id)
        setMessages(history)
        if (current.status === 'Transferred') {
          const detail = await apiClient.getAdminSession(current.id)
          setHandoff(detail.handoff ?? null)
        }
      }
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const sendMessage = async (content: string) => {
    if (!content.trim() || sending) return
    setSending(true)
    setError(null)
    try {
      const response = await apiClient.sendMessage(DEMO_CUSTOMER_ID, channel, content.trim())
      setMessages(response.messages)
      setHandoff(response.handoff ?? null)
      setContextRestored(response.contextRestored)
      setSession({
        id: response.sessionId,
        protocol: response.protocol,
        customerId: DEMO_CUSTOMER_ID,
        customerName: customer?.name ?? 'Lucas',
        initialChannel: session?.initialChannel ?? channel,
        currentChannel: response.currentChannel,
        status: response.status,
        detectedIntent: response.detectedIntent,
        createdAt: session?.createdAt ?? new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        contextRestored: response.contextRestored,
        context: response.context,
      })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const changeChannel = async (next: ChannelType) => {
    setChannel(next)
    if (!session) return

    setSending(true)
    setError(null)
    try {
      const updated = await apiClient.changeChannel(session.id, next)
      setSession(updated)
      setContextRestored(updated.contextRestored)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const requestHandoff = async () => {
    if (!session || sending) return
    setSending(true)
    setError(null)
    try {
      const created = await apiClient.createHandoff(session.id)
      setHandoff(created)
      setSession({ ...session, status: 'Transferred' })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const startNewAttendance = () => {
    setSession(null)
    setMessages([])
    setHandoff(null)
    setContextRestored(false)
    setChannel('AppClaro')
    setError(null)
  }

  return {
    customer,
    session,
    messages,
    handoff,
    channel,
    contextRestored,
    loading,
    sending,
    error,
    sendMessage,
    changeChannel,
    requestHandoff,
    startNewAttendance,
    reload: load,
  }
}
