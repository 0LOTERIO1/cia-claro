export const DEMO_CUSTOMER_ID = 'CLIENTE-001'

export function formatChannel(channel: string): string {
  return channel === 'WhatsApp' ? 'WhatsApp' : 'App Claro'
}

export function formatStatus(status: string): string {
  switch (status) {
    case 'Active':
      return 'Ativo'
    case 'Resolved':
      return 'Resolvido'
    case 'Transferred':
      return 'Transferido'
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
