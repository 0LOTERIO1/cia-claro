import axios, { isAxiosError } from 'axios'
import type {
  AdminSessionDetailDto,
  AgentQueueItemDto,
  AgentSessionDetailDto,
  CustomerDto,
  DashboardDto,
  DepartmentType,
  HandoffDto,
  LoginResponse,
  MessageDto,
  SendMessageResponse,
  SessionDto,
  UserDto,
} from '../types/api'

const TOKEN_KEY = 'cia.token'
const USER_KEY = 'cia.user'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5080',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function getStoredUser(): UserDto | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as UserDto
  } catch {
    return null
  }
}

export function persistAuth(token: string, user: UserDto) {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(USER_KEY, JSON.stringify(user))
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

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
  login: async (email: string, password: string) => {
    const { data } = await api.post<LoginResponse>('/api/auth/login', { email, password })
    return data
  },
  me: async () => {
    const { data } = await api.get<UserDto>('/api/auth/me')
    return data
  },
  getCustomer: async (id: string) => {
    const { data } = await api.get<CustomerDto>(`/api/customers/${id}`)
    return data
  },
  getSessionsByCustomer: async (customerId: string) => {
    const { data } = await api.get<SessionDto[]>(`/api/sessions/customer/${customerId}`)
    return data
  },
  getSession: async (sessionId: string) => {
    const { data } = await api.get<SessionDto>(`/api/sessions/${sessionId}`)
    return data
  },
  getMessages: async (sessionId: string) => {
    const { data } = await api.get<MessageDto[]>(`/api/sessions/${sessionId}/messages`)
    return data
  },
  sendMessage: async (customerId: string, content: string) => {
    const { data } = await api.post<SendMessageResponse>('/api/chat/message', {
      customerId,
      channel: 'AppClaro',
      content,
    })
    return data
  },
  changeDepartment: async (sessionId: string, department: DepartmentType, reason: string) => {
    const { data } = await api.post<SessionDto>(`/api/sessions/${sessionId}/department`, {
      department,
      reason,
    })
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
  getAgentQueue: async () => {
    const { data } = await api.get<AgentQueueItemDto[]>('/api/agent/queue')
    return data
  },
  getAgentMine: async () => {
    const { data } = await api.get<AgentQueueItemDto[]>('/api/agent/mine')
    return data
  },
  assumeRequest: async (requestId: string) => {
    const { data } = await api.post<AgentSessionDetailDto>(`/api/agent/requests/${requestId}/assume`)
    return data
  },
  getAgentRequest: async (requestId: string) => {
    const { data } = await api.get<AgentSessionDetailDto>(`/api/agent/requests/${requestId}`)
    return data
  },
  sendAgentMessage: async (sessionId: string, content: string) => {
    const { data } = await api.post<MessageDto[]>(`/api/agent/sessions/${sessionId}/messages`, { content })
    return data
  },
  finishRequest: async (requestId: string) => {
    const { data } = await api.post<AgentSessionDetailDto>(`/api/agent/requests/${requestId}/finish`)
    return data
  },
}
