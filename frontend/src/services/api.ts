import axios, { isAxiosError } from 'axios'
import type {
  AdminSessionDetailDto,
  ChannelType,
  CustomerDto,
  DashboardDto,
  HandoffDto,
  MessageDto,
  SendMessageResponse,
  SessionDto,
} from '../types/api'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5080',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
})

export function getErrorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    if (!error.response) {
      return 'Não foi possível conectar ao servidor. Verifique se a API está em execução.'
    }

    const data = error.response.data as { error?: string } | undefined
    return data?.error ?? `Erro ${error.response.status} ao comunicar com a API.`
  }

  return 'Ocorreu um erro inesperado.'
}

export const apiClient = {
  getCustomer: async (id: string) => {
    const { data } = await api.get<CustomerDto>(`/api/customers/${id}`)
    return data
  },
  getSessionsByCustomer: async (customerId: string) => {
    const { data } = await api.get<SessionDto[]>(`/api/sessions/customer/${customerId}`)
    return data
  },
  getMessages: async (sessionId: string) => {
    const { data } = await api.get<MessageDto[]>(`/api/sessions/${sessionId}/messages`)
    return data
  },
  sendMessage: async (customerId: string, channel: ChannelType, content: string) => {
    const { data } = await api.post<SendMessageResponse>('/api/chat/message', {
      customerId,
      channel,
      content,
    })
    return data
  },
  changeChannel: async (sessionId: string, channel: ChannelType) => {
    const { data } = await api.post<SessionDto>(`/api/sessions/${sessionId}/channel`, { channel })
    return data
  },
  createHandoff: async (sessionId: string) => {
    const { data } = await api.post<HandoffDto>(`/api/sessions/${sessionId}/handoff`)
    return data
  },
  getDashboard: async () => {
    const { data } = await api.get<DashboardDto>('/api/admin/dashboard')
    return data
  },
  getAdminSessions: async () => {
    const { data } = await api.get<SessionDto[]>('/api/admin/sessions')
    return data
  },
  getAdminSession: async (id: string) => {
    const { data } = await api.get<AdminSessionDetailDto>(`/api/admin/sessions/${id}`)
    return data
  },
}
