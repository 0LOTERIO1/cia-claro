# Texto explicativo — Sprint 3

## O que foi desenvolvido

Foi implementada a versão 1 funcional da CIA — Claro Inteligência Artificial, uma aplicação de atendimento omnicanal para o Challenge da FIAP em parceria com a Claro.

A entrega não é um protótipo visual isolado. Existe frontend, backend, banco de dados, API REST e persistência do atendimento. O fluxo principal permite que o cliente Lucas (`CLIENTE-001`) inicie um suporte de internet no App Claro e continue no WhatsApp sem repetir o que já informou.

## Arquitetura

A solução foi organizada em camadas:

- React consome a API.
- ASP.NET Core recebe as requisições, aplica regras de negócio e persiste dados.
- Entity Framework Core acessa o PostgreSQL.

Controllers não concentram regra de negócio. A orquestração fica em serviços, e o acesso a dados fica em repositórios.

## Frontend

A interface possui duas áreas:

- `/` : chat do cliente, com protocolo, canal, histórico, seletor App Claro/WhatsApp e transbordo.
- `/admin` : dashboard com totais, quantidade por canal e tabela de sessões. O detalhe de cada atendimento mostra contexto, mensagens e resumo de transbordo.

O visual usa vermelho como destaque e altera a identidade ao mudar para WhatsApp, para tornar a troca de canal evidente na demonstração.

## Backend

O backend é uma Web API .NET 8. Os principais serviços são:

- `ConversationService` para o ciclo completo da mensagem
- `ContextService` para o estado da jornada
- `IntentService` para classificação da intenção
- `AiService` para geração de resposta
- `HandoffService` para transbordo humano
- `DashboardService` para a área administrativa

## Banco de dados

O PostgreSQL armazena cliente, sessão, mensagens, contexto e transbordo.

A sessão recebe um protocolo único, por exemplo `CIA-20260827-0001`. Esse protocolo permanece igual depois da troca de canal.

O seed cria o cliente fictício Lucas / `CLIENTE-001`.

## API

A comunicação é REST/JSON. Durante o desenvolvimento, os endpoints podem ser testados em `/swagger`.

O health check `GET /api/health` confirma que a API está no ar.

## Contexto

O contexto é o componente central da proposta. Ele registra:

- tipo do problema
- se o modem já foi reiniciado
- dados adicionais da última atualização

Quando o cliente escreve “Quero continuar meu atendimento” no WhatsApp, o backend recupera esse contexto no PostgreSQL e monta a resposta. A frase não é hardcoded no React.

## Inteligência artificial

A identificação de intenção e a geração de resposta usam a abstração `IAiProvider`.

Nesta versão, o fluxo completo de demonstração funciona com o provedor local, sem chave externa. Se uma API de IA for configurada por variável de ambiente, o `ExternalAiProvider` pode ser utilizado sem reescrever os controllers.

## Omnicanalidade

App Claro e WhatsApp são canais da mesma sessão. A troca atualiza `CurrentChannel` e preserva:

- o mesmo `SessionId`
- o mesmo protocolo
- o mesmo histórico
- o mesmo contexto

## Transbordo

O cliente pode pedir um atendente por texto ou pelo botão da interface. O backend gera um resumo estruturado, persiste o `Handoff` e altera o status da sessão para `Transferred`.

## Dashboard

A área administrativa consulta a API e mostra a operação real: quantidade de atendimentos, status, canal, intenção e o detalhe completo da sessão.

## Tecnologias

- React + Vite + TypeScript
- Axios
- ASP.NET Core 8
- Entity Framework Core
- PostgreSQL
- Swagger / OpenAPI
- xUnit
