# Roteiro de demonstração

Vídeo técnico de 6 a 8 minutos. Não é pitch comercial.

## 0:00–0:30 — Apresentação técnica

“Apresentamos a versão funcional da CIA — Claro Inteligência Artificial, composta por frontend React, backend ASP.NET Core e banco PostgreSQL. A CIA é a camada central de contexto entre as áreas de atendimento.”

## 0:30–1:10 — Arquitetura

Mostrar `docs/ARQUITETURA.md`.

Explicar:

- a CIA é dona do contexto
- os bots não são donos do histórico
- orquestrador decide a área sem criar outra sessão

## 1:10–2:00 — Triagem

Abrir o frontend.

Mostrar Lucas / `CLIENTE-001` e área **Triagem**.

Enviar:

`Minha internet não está funcionando.`

Mostrar redirecionamento para **Suporte Técnico** e o protocolo criado.

## 2:00–3:00 — Suporte Técnico

O técnico pergunta se o modem já foi reiniciado.

Enviar:

`Já reiniciei e continua sem internet.`

Mostrar no contexto:

- `IssueType = InternetConnection`
- `ModemRestarted = true`
- `InternetStillDown = true`

## 3:00–4:00 — Troca de Modem

Mostrar a transferência automática para **Troca de Modem**.

O novo bot já sabe:

- problema de internet
- modem reiniciado
- falha persistente

Ele **não** pergunta “qual é o seu problema?” nem “você já reiniciou o modem?”.

## 4:00–5:00 — Financeiro

Enviar:

`Essa troca vai gerar alguma cobrança?`

Mostrar redirecionamento para **Financeiro**. O financeiro continua sabendo a jornada.

## 5:00–5:50 — API real

Abrir Swagger / logs do backend.

Mostrar `GET /api/health` e a sessão com `currentDepartment` e `transfers`.

## 5:50–6:40 — Atendimento humano

Enviar `Quero falar com um atendente.` ou clicar no botão.

Mostrar o resumo com a jornada completa.

## 6:40–7:20 — Dashboard

Abrir `/admin` e o detalhe da sessão: contexto, histórico e jornada.

## 7:20–7:40 — Encerramento

A CIA preserva o mesmo protocolo, a mesma sessão e o mesmo contexto entre as áreas. O cliente não precisa repetir o que já informou.
