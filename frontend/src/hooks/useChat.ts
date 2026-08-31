import { useCallback, useEffect, useState } from 'react'
import { apiClient, getErrorMessage } from '../services/api'
import { DEMO_CUSTOMER_ID } from '../services/labels'
import type {
  CustomerDto,
  DepartmentType,
  HandoffDto,
  MessageDto,
  SessionDto,
  TransferDto,
} from '../types/api'

export function useChat() {
  const [customer, setCustomer] = useState<CustomerDto | null>(null)
  const [session, setSession] = useState<SessionDto | null>(null)
  const [messages, setMessages] = useState<MessageDto[]>([])
  const [handoff, setHandoff] = useState<HandoffDto | null>(null)
  const [transfers, setTransfers] = useState<TransferDto[]>([])
  const [contextRestored, setContextRestored] = useState(false)
  const [transferNotice, setTransferNotice] = useState<string | null>(null)
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
        setTransfers(current.transfers ?? [])
        const history = await apiClient.getMessages(current.id)
        setMessages(history)
        if (current.status === 'Transferred') {
          const detail = await apiClient.getAdminSession(current.id)
          setHandoff(detail.handoff ?? null)
          setTransfers(detail.transfers ?? current.transfers ?? [])
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
      const response = await apiClient.sendMessage(DEMO_CUSTOMER_ID, content.trim())
      setMessages(response.messages)
      setHandoff(response.handoff ?? null)
      setContextRestored(response.contextRestored)
      setTransferNotice(response.transferNotice ?? null)
      setTransfers(response.transfers ?? [])
      setSession({
        id: response.sessionId,
        protocol: response.protocol,
        customerId: DEMO_CUSTOMER_ID,
        customerName: customer?.name ?? 'Lucas',
        initialChannel: session?.initialChannel ?? 'AppClaro',
        currentChannel: response.currentChannel,
        currentDepartment: response.currentDepartment,
        previousDepartment: response.previousDepartment,
        status: response.status,
        detectedIntent: response.detectedIntent,
        createdAt: session?.createdAt ?? new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        contextRestored: response.contextRestored,
        departmentChanged: response.departmentChanged,
        context: response.context,
        transfers: response.transfers ?? [],
      })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const changeDepartment = async (department: DepartmentType, reason: string) => {
    if (!session || sending) return
    setSending(true)
    setError(null)
    try {
      const updated = await apiClient.changeDepartment(session.id, department, reason)
      setSession(updated)
      setTransfers(updated.transfers ?? [])
      setContextRestored(updated.contextRestored)
      setTransferNotice(`Seu contexto foi transferido para a nova área. Continuando seu atendimento com o contexto anterior.`)
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
      setSession({
        ...session,
        status: 'Transferred',
        currentDepartment: 'HumanAgent',
        previousDepartment: session.currentDepartment,
      })
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
    setTransfers([])
    setContextRestored(false)
    setTransferNotice(null)
    setError(null)
  }

  return {
    customer,
    session,
    messages,
    handoff,
    transfers,
    contextRestored,
    transferNotice,
    loading,
    sending,
    error,
    sendMessage,
    changeDepartment,
    requestHandoff,
    startNewAttendance,
    reload: load,
  }
}
