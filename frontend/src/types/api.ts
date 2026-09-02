export type ChannelType = 'AppClaro' | 'WhatsApp'
export type DepartmentType =
  | 'Triage'
  | 'TechnicalSupport'
  | 'ModemReplacement'
  | 'Financial'
  | 'HumanAgent'
export type SessionStatus = 'Active' | 'Resolved' | 'Transferred' | 'WaitingForAgent'
export type MessageSender = 'Customer' | 'Assistant' | 'HumanAgent'
export type UserRole = 'Customer' | 'Agent' | 'Admin'
export type HumanAgentRequestStatus = 'Waiting' | 'Assigned' | 'Finished'
export type IntentType =
  | 'Unknown'
  | 'Greeting'
  | 'InternetProblem'
  | 'ModemRestarted'
  | 'ContinueSupport'
  | 'HumanHandoff'
  | 'ModemReplacement'
  | 'BillingQuestion'
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
  internetStillDown: boolean
  originalProblem?: string | null
  troubleshootingPerformed?: string | null
  currentRequest?: string | null
  importantFacts?: string | null
  contextSummary?: string | null
  additionalData?: string | null
  updatedAt: string
}

export interface TransferDto {
  id: string
  sessionId: string
  fromDepartment: DepartmentType
  toDepartment: DepartmentType
  reason: string
  createdAt: string
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
  currentDepartment: DepartmentType
  previousDepartment?: DepartmentType | null
  status: SessionStatus
  detectedIntent: IntentType
  createdAt: string
  updatedAt: string
  contextRestored: boolean
  departmentChanged: boolean
  context?: ContextDto | null
  humanRequestStatus?: HumanAgentRequestStatus | null
  transfers: TransferDto[]
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
  currentDepartment: DepartmentType
  previousDepartment?: DepartmentType | null
  contextRestored: boolean
  departmentChanged: boolean
  transferNotice?: string | null
  context?: ContextDto | null
  assistantMessage?: MessageDto | null
  handoff?: HandoffDto | null
  humanAgentRequest?: HumanAgentRequestDto | null
  messages: MessageDto[]
  transfers: TransferDto[]
}

export interface ChannelCountDto {
  channel: ChannelType
  count: number
}

export interface DepartmentCountDto {
  department: DepartmentType
  count: number
}

export interface DashboardDto {
  totalSessions: number
  activeSessions: number
  resolvedSessions: number
  transferredSessions: number
  sessionsByChannel: ChannelCountDto[]
  sessionsByDepartment: DepartmentCountDto[]
}

export interface AdminSessionDetailDto {
  session: SessionDto
  customer: CustomerDto
  context?: ContextDto | null
  messages: MessageDto[]
  handoff?: HandoffDto | null
  transfers: TransferDto[]
}

export interface UserDto {
  id: string
  name: string
  email: string
  role: UserRole
  customerId?: string | null
}

export interface LoginResponse {
  token: string
  user: UserDto
}

export interface HumanAgentRequestDto {
  id: string
  sessionId: string
  status: HumanAgentRequestStatus
  assignedAgentId?: string | null
  assignedAgentName?: string | null
  createdAt: string
  assignedAt?: string | null
  finishedAt?: string | null
}

export interface AgentQueueItemDto {
  requestId: string
  sessionId: string
  protocol: string
  customerName: string
  customerId: string
  problem: string
  contextFacts: string[]
  contextSummary?: string | null
  status: HumanAgentRequestStatus
  createdAt: string
}

export interface AgentSessionDetailDto {
  request: AgentQueueItemDto
  session: SessionDto
  customer: CustomerDto
  context?: ContextDto | null
  messages: MessageDto[]
  transfers: TransferDto[]
  handoff?: HandoffDto | null
}
