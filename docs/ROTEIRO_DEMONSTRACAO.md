# Roteiro de demonstração

Vídeo técnico de 6 a 8 minutos. Não é pitch comercial.

## 0:00–0:30 — Apresentação técnica

Falar:

“Apresentamos a versão funcional da CIA — Claro Inteligência Artificial, composta por frontend React, backend ASP.NET Core e banco PostgreSQL.”

Mostrar a estrutura do repositório: `/frontend`, `/backend`, `/docs`.

## 0:30–1:20 — Arquitetura

Abrir `docs/ARQUITETURA.md` e o diagrama Mermaid.

Explicar rapidamente:

- React chama a API REST
- Controllers delegam para Services
- Repositories persistem com EF Core no PostgreSQL
- `ContextService` guarda o estado da jornada

## 1:20–2:30 — Início no App Claro

Abrir http://localhost:5173.

Mostrar:

- Cliente Lucas
- Customer ID `CLIENTE-001`
- Canal **APP CLARO**

Enviar:

`Minha internet não está funcionando.`

Mostrar a resposta da CIA e o protocolo criado.

## 2:30–3:20 — Contexto do modem

Enviar:

`Sim, já reiniciei o modem e continua sem internet.`

Mostrar na interface a intenção e, se possível, no Swagger ou no detalhe administrativo que o contexto ficou com:

- `IssueType = InternetConnection`
- `ModemRestarted = true`

## 3:20–4:30 — Continuidade no WhatsApp

Trocar o seletor para **WhatsApp**.

Destacar que o protocolo não mudou.

Enviar:

`Quero continuar meu atendimento.`

Mostrar a mensagem:

“Contexto do atendimento anterior recuperado.”

A resposta da CIA deve mencionar a internet residencial e a reinicialização do modem.

## 4:30–5:15 — Provar que a API é real

Abrir http://localhost:5080/swagger.

Executar `GET /api/health`.

Opcionalmente mostrar `GET /api/sessions/customer/CLIENTE-001`.

Mostrar os logs do backend:

- Customer identified
- Session created
- Intent detected
- Channel changed
- Context restored

## 5:15–6:00 — Transbordo

Clicar em **Falar com atendente** ou enviar:

`Quero falar com um atendente.`

Mostrar o resumo estruturado e o status **Transferido**.

## 6:00–6:50 — Dashboard

Abrir http://localhost:5173/admin.

Mostrar totais, canais e a tabela.

Entrar no atendimento e mostrar histórico, contexto e resumo.

## 6:50–7:30 — Tecnologias e encerramento

Encerrar com a stack:

React + TypeScript, ASP.NET Core .NET 8, Entity Framework Core, PostgreSQL, REST e Swagger.

Frase final:

“O cliente inicia no App Claro e continua no WhatsApp sem repetir o que já informou, porque o contexto ficou persistido no backend.”
