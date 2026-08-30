export type ChannelType = 'AppClaro' | 'WhatsApp'
export type SessionStatus = 'Active' | 'Resolved' | 'Transferred'
export type MessageSender = 'Customer' | 'Assistant' | 'HumanAgent'
export type IntentType =
  | 'Unknown'
  | 'Greeting'
  | 'InternetProblem'
  | 'ModemRestarted'
  | 'ContinueSupport'
  | 'HumanHandoff'
export type IssueType = 'None' | 'InternetConnection'
export type HandoffStatus = 'Pending' | 'Completed'

export interface CustomerDto {
  id: string
  name: string
  phone: string
  createdAt: string
}

export interface ContextDto {
  id: string
  sessionId: string
  issueType: IssueType
  modemRestarted: boolean
  additionalData?: string | null
  updatedAt: string
}

export interface MessageDto {
  id: string
  sessionId: string
  sender: MessageSender
  channel: ChannelType
  content: string
  createdAt: string
}

export interface SessionDto {
  id: string
  protocol: string
  customerId: string
  customerName: string
  initialChannel: ChannelType
  currentChannel: ChannelType
  status: SessionStatus
  detectedIntent: IntentType
  createdAt: string
  updatedAt: string
  contextRestored: boolean
  context?: ContextDto | null
}

export interface HandoffDto {
  id: string
  sessionId: string
  summary: string
  status: HandoffStatus
  createdAt: string
}

export interface SendMessageResponse {
  sessionId: string
  protocol: string
  status: SessionStatus
  detectedIntent: IntentType
  currentChannel: ChannelType
  contextRestored: boolean
  context?: ContextDto | null
  assistantMessage: MessageDto
  handoff?: HandoffDto | null
  messages: MessageDto[]
}

export interface ChannelCountDto {
  channel: ChannelType
  count: number
}

export interface DashboardDto {
  totalSessions: number
  activeSessions: number
  resolvedSessions: number
  transferredSessions: number
  sessionsByChannel: ChannelCountDto[]
}

export interface AdminSessionDetailDto {
  session: SessionDto
  customer: CustomerDto
  context?: ContextDto | null
  messages: MessageDto[]
  handoff?: HandoffDto | null
}
