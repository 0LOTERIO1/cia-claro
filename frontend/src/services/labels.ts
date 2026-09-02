export const DEMO_CUSTOMER_ID = 'CLIENTE-001'

export function formatChannel(channel: string): string {
  return channel === 'WhatsApp' ? 'WhatsApp' : 'App Claro'
}

export function formatDepartment(department?: string | null): string {
  switch (department) {
    case 'Triage':
      return 'Triagem'
    case 'TechnicalSupport':
      return 'Suporte Técnico'
    case 'ModemReplacement':
      return 'Troca de Modem'
    case 'Financial':
      return 'Financeiro'
    case 'HumanAgent':
      return 'Atendimento Humano'
    default:
      return department ?? 'Triagem'
  }
}

export function formatStatus(status: string): string {
  switch (status) {
    case 'Active':
      return 'Ativo'
    case 'Resolved':
      return 'Resolvido'
    case 'Transferred':
      return 'Com atendente'
    case 'WaitingForAgent':
      return 'Aguardando atendente'
    default:
      return status
  }
}

export function formatIntent(intent: string): string {
  switch (intent) {
    case 'InternetProblem':
      return 'Problema de internet'
    case 'ModemRestarted':
      return 'Modem reiniciado'
    case 'ModemReplacement':
      return 'Troca de modem'
    case 'BillingQuestion':
      return 'Dúvida financeira'
    case 'ContinueSupport':
      return 'Continuar atendimento'
    case 'HumanHandoff':
      return 'Transbordo humano'
    case 'Greeting':
      return 'Saudação'
    default:
      return 'Não identificada'
  }
}

export function formatIssue(issue?: string | null): string {
  return issue === 'InternetConnection' ? 'Internet residencial sem conexão' : 'Não identificado'
}

export function formatDateTime(value: string): string {
  return new Date(value).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}
