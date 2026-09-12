import { useCallback, useEffect, useRef, useState } from 'react'
import { apiClient, getErrorMessage } from '../services/api'
import { sameMessageSnapshot } from '../services/messages'
import type {
  CustomerChannelDto,
  CustomerDto,
  DepartmentType,
  HandoffDto,
  MessageDto,
  ServiceRatingDto,
  SessionDto,
  TelegramLinkCodeDto,
  TransferDto,
} from '../types/api'

function isOpenStatus(status: SessionDto['status'], humanRequestStatus?: SessionDto['humanRequestStatus']) {
  return (
    status === 'Active' ||
    status === 'WaitingForAgent' ||
    (status === 'Transferred' && (humanRequestStatus === 'Waiting' || humanRequestStatus === 'Assigned'))
  )
}

function isHumanSession(session: SessionDto | null | undefined) {
  if (!session) return false
  return (
    session.status === 'WaitingForAgent' ||
    (session.status === 'Transferred' &&
      (session.humanRequestStatus === 'Waiting' || session.humanRequestStatus === 'Assigned'))
  )
}

function shouldAutoResume(session: SessionDto | null | undefined) {
  if (!session) return false
  if (session.status === 'Resolved') return true
  if (session.currentChannel === 'WebPortal') return true
  if (session.initialChannel !== 'Telegram') return true
  return false
}

function sameSessionSnapshot(current: SessionDto, next: SessionDto) {
  return (
    current.status === next.status &&
    current.currentDepartment === next.currentDepartment &&
    current.currentChannel === next.currentChannel &&
    current.updatedAt === next.updatedAt &&
    current.humanRequestStatus === next.humanRequestStatus &&
    current.canRate === next.canRate &&
    current.closureReason === next.closureReason &&
    current.rating?.score === next.rating?.score &&
    current.rating?.comment === next.rating?.comment
  )
}

export function useChat(customerId: string | null) {
  const [customer, setCustomer] = useState<CustomerDto | null>(null)
  const [session, setSession] = useState<SessionDto | null>(null)
  const [messages, setMessages] = useState<MessageDto[]>([])
  const [handoff, setHandoff] = useState<HandoffDto | null>(null)
  const [transfers, setTransfers] = useState<TransferDto[]>([])
  const [channels, setChannels] = useState<CustomerChannelDto[]>([])
  const [linkCode, setLinkCode] = useState<TelegramLinkCodeDto | null>(null)
  const [resumed, setResumed] = useState(false)
  const [contextRestored, setContextRestored] = useState(false)
  const [transferNotice, setTransferNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [linking, setLinking] = useState(false)
  const [unlinking, setUnlinking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [ratingError, setRatingError] = useState<string | null>(null)
  const [ratingSubmitting, setRatingSubmitting] = useState(false)
  const startingFreshRef = useRef(false)

  const applySnapshot = useCallback(
    (nextSession: SessionDto | null, nextMessages: MessageDto[], autoOpen: boolean) => {
      setSession(nextSession)
      setTransfers(nextSession?.transfers ?? [])
      if (autoOpen && nextSession) {
        setMessages(nextMessages)
        setResumed(true)
        if (nextSession.status === 'Transferred' || nextSession.status === 'WaitingForAgent') {
          void apiClient.getAdminSession(nextSession.id).then((detail) => {
            setHandoff(detail.handoff ?? null)
            setTransfers(detail.transfers ?? nextSession.transfers ?? [])
          }).catch(() => undefined)
        }
      } else if (!nextSession) {
        setMessages([])
        setResumed(false)
      }
    },
    [],
  )

  const load = useCallback(async () => {
    if (!customerId) {
      setLoading(false)
      return
    }

    setLoading(true)
    setError(null)
    try {
      const [customerData, snapshot] = await Promise.all([
        apiClient.getCustomer(customerId),
        apiClient.getActiveSession(),
      ])
      setCustomer(customerData)
      setChannels(snapshot.channels ?? [])
      applySnapshot(snapshot.session ?? null, snapshot.messages ?? [], shouldAutoResume(snapshot.session))
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }, [applySnapshot, customerId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!customerId) return

    const timer = window.setInterval(async () => {
      try {
        const snapshot = await apiClient.getActiveSession()
        setChannels(snapshot.channels ?? [])
        if (snapshot.channels?.some((item) => item.channel === 'Telegram' && item.connected)) {
          setLinkCode(null)
        }

        const nextSession = snapshot.session ?? null
        if (startingFreshRef.current) {
          if (!nextSession || !isOpenStatus(nextSession.status, nextSession.humanRequestStatus)) {
            return
          }
          startingFreshRef.current = false
          applySnapshot(nextSession, snapshot.messages ?? [], true)
          return
        }

        setSession((current) => {
          if (current && nextSession && sameSessionSnapshot(current, nextSession)) {
            return current
          }
          return nextSession
        })
        setTransfers(nextSession?.transfers ?? [])

        if (resumed && nextSession && isOpenStatus(nextSession.status, nextSession.humanRequestStatus)) {
          const history = snapshot.messages ?? []
          setMessages((current) => (sameMessageSnapshot(current, history) ? current : history))
        }
      } catch {
        // Mantém a tela utilizável se um ciclo de polling falhar.
      }
    }, 2500)

    return () => window.clearInterval(timer)
  }, [customerId, resumed])

  const sendMessage = async (content: string) => {
    if (!content.trim() || sending || !customerId) return
    setSending(true)
    setError(null)
    try {
      const response = await apiClient.sendCustomerMessage(content.trim())
      setMessages(response.messages)
      setHandoff(response.handoff ?? null)
      setContextRestored(response.contextRestored)
      setTransferNotice(response.transferNotice ?? null)
      setTransfers(response.transfers ?? [])
      setResumed(true)
      startingFreshRef.current = false
      setSession({
        id: response.sessionId,
        protocol: response.protocol,
        customerId,
        customerName: customer?.name ?? 'Cliente',
        initialChannel: session?.initialChannel ?? 'WebPortal',
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
        canRate: false,
        rating: null,
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
      setTransferNotice('Seu contexto foi transferido para a nova área. Continuando seu atendimento com o contexto anterior.')
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
        status: 'WaitingForAgent',
        currentDepartment: 'HumanAgent',
        previousDepartment: session.currentDepartment,
      })
      setTransferNotice('Você entrou na fila de atendimento humano. Um funcionário da Claro assumirá este protocolo em instantes.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const connectTelegram = async () => {
    setLinking(true)
    setError(null)
    try {
      const generated = await apiClient.createTelegramLinkCode()
      setLinkCode(generated)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLinking(false)
    }
  }

  const disconnectTelegram = async () => {
    if (unlinking) return
    setUnlinking(true)
    setError(null)
    try {
      const result = await apiClient.unlinkTelegram()
      const snapshot = await apiClient.getActiveSession()
      setChannels(snapshot.channels ?? [])
      setLinkCode(null)
      applySnapshot(snapshot.session ?? null, snapshot.messages ?? [], Boolean(snapshot.session))
      setTransferNotice(result.message)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setUnlinking(false)
    }
  }

  const continueAttendance = async () => {
    if (!session || sending) return
    setSending(true)
    setError(null)
    try {
      const snapshot = await apiClient.resumeActiveSession()
      setChannels(snapshot.channels ?? [])
      applySnapshot(snapshot.session ?? null, snapshot.messages ?? [], true)
      setContextRestored(true)
      setTransferNotice('Continuando o atendimento iniciado em outro canal. O histórico e o contexto foram mantidos.')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const startNewAttendance = async () => {
    if (session && session.status === 'Active' && !isHumanSession(session)) {
      setSending(true)
      setError(null)
      try {
        const result = await apiClient.restartSession()
        startingFreshRef.current = false
        setChannels(result.snapshot.channels ?? [])
        applySnapshot(result.snapshot.session ?? null, result.snapshot.messages ?? [], true)
        setHandoff(null)
        setContextRestored(false)
        setTransferNotice(result.message)
        setRatingError(null)
      } catch (err) {
        setError(getErrorMessage(err))
      } finally {
        setSending(false)
      }
      return
    }

    startingFreshRef.current = true
    setSession(null)
    setMessages([])
    setHandoff(null)
    setTransfers([])
    setContextRestored(false)
    setTransferNotice(null)
    setResumed(false)
    setError(null)
    setRatingError(null)
  }

  const endAttendance = async () => {
    if (!session || sending || isHumanSession(session) || session.status !== 'Active') return
    setSending(true)
    setError(null)
    try {
      const result = await apiClient.endSession()
      startingFreshRef.current = true
      setChannels(result.snapshot.channels ?? [])
      applySnapshot(null, [], false)
      setHandoff(null)
      setTransfers([])
      setContextRestored(false)
      setResumed(false)
      setTransferNotice(result.message)
      setRatingError(null)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const submitRating = async (score: number, comment: string) => {
    if (!session || ratingSubmitting) return
    setRatingSubmitting(true)
    setRatingError(null)
    try {
      const result = await apiClient.submitServiceRating(session.id, score, comment)
      const submitted: ServiceRatingDto | null = result.rating ?? {
        id: session.id,
        sessionId: session.id,
        score,
        comment: comment.trim() || null,
        createdAt: new Date().toISOString(),
      }
      setSession({
        ...session,
        canRate: false,
        rating: submitted,
      })
      setTransferNotice(result.message)
    } catch (err) {
      setRatingError(getErrorMessage(err))
    } finally {
      setRatingSubmitting(false)
    }
  }

  const telegram = channels.find((item) => item.channel === 'Telegram')

  return {
    customer,
    session,
    messages,
    handoff,
    transfers,
    channels,
    telegram,
    linkCode,
    resumed,
    contextRestored,
    transferNotice,
    loading,
    sending,
    linking,
    unlinking,
    error,
    ratingError,
    ratingSubmitting,
    sendMessage,
    changeDepartment,
    requestHandoff,
    connectTelegram,
    disconnectTelegram,
    continueAttendance,
    startNewAttendance,
    endAttendance,
    submitRating,
    reload: load,
  }
}
