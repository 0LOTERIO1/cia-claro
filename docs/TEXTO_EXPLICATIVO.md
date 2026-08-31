# Texto explicativo — Sprint 3

## O que foi desenvolvido

Foi evoluída a CIA — Claro Inteligência Artificial — para funcionar como camada central de contexto entre áreas de atendimento da Claro.

A CIA integra diferentes fluxos e áreas, preservando o contexto do cliente durante redirecionamentos para evitar que ele precise repetir informações já fornecidas.

O problema resolvido não é a troca de canal App Claro → WhatsApp. O problema é a perda de contexto quando o cliente sai de um bot ou setor e entra em outro.

## Arquitetura

```
Cliente → Frontend → API → Orquestrador CIA → Contexto central → Triagem / Técnico / Troca de Modem / Financeiro / Humano
```

Controllers recebem HTTP. Serviços aplicam a regra. Repositórios persistem no PostgreSQL.

## Frontend

- `/` : chat contínuo, área atual, jornada do atendimento e transbordo.
- `/admin` : indicadores, tabela de sessões e detalhe com contexto e jornada.

A troca de área não limpa o chat nem gera outro protocolo.

## Backend

Serviços principais:

- `ConversationService`
- `ContextService`
- `IntentService`
- `OrchestrationService`
- `AiService`
- `HandoffService`
- `DashboardService`

## Banco de dados

O PostgreSQL armazena cliente, sessão, mensagens, contexto, transferências entre departamentos e transbordo humano.

A sessão inicia na Triagem e recebe um protocolo único. Esse protocolo permanece igual depois de cada redirecionamento.

## Contexto

O contexto registra o problema original, o reinício do modem, a persistência da falha e o pedido atual. Qualquer área nova lê esses dados no banco. A resposta não é hardcoded no React.

## Orquestração

O `OrchestrationService` decide a próxima área:

- internet sem conexão → Suporte Técnico
- modem já reiniciado e problema persistente → Troca de Modem
- dúvida de cobrança → Financeiro
- pedido de atendente → Atendimento Humano

## Transbordo

O resumo humano inclui a jornada completa e os procedimentos já realizados.

## Tecnologias

React, Vite, TypeScript, ASP.NET Core 8, Entity Framework Core, PostgreSQL, Swagger e xUnit.
