# CIA — Claro Inteligência Artificial

Aplicação funcional da Sprint 3 do Challenge FIAP em parceria com a Claro.

A CIA é uma solução de atendimento omnicanal. O cliente inicia o suporte no App Claro, troca para o WhatsApp e continua a mesma sessão, com o mesmo protocolo e com o contexto já registrado no PostgreSQL.

## O que esta versão demonstra

- Frontend React chamando a API ASP.NET Core
- Persistência real em PostgreSQL
- Sessão, mensagens, contexto e transbordo
- Troca de canal App Claro → WhatsApp sem criar outra sessão
- Recuperação de contexto no backend
- Dashboard administrativo alimentado pela API

## Arquitetura

```
Cliente
  → App Claro / WhatsApp (simulação no React)
    → REST API JSON
      → ASP.NET Core Controllers
        → Services
          → Repositories
            → Entity Framework Core
              → PostgreSQL
```

Serviços especializados:

- `ConversationService`
- `ContextService`
- `IntentService`
- `AiService`
- `HandoffService`
- `DashboardService`

Documentação detalhada:

- [docs/ARQUITETURA.md](docs/ARQUITETURA.md)
- [docs/TEXTO_EXPLICATIVO.md](docs/TEXTO_EXPLICATIVO.md)
- [docs/ROTEIRO_DEMONSTRACAO.md](docs/ROTEIRO_DEMONSTRACAO.md)
- [docs/DEPLOY.md](docs/DEPLOY.md) — publicação na Vercel (frontend) e API na nuvem

## Publicar na web

O frontend pode ir para a **Vercel**. A API .NET e o PostgreSQL precisam de outro host (Render, Railway, Azure, etc.).

Passo a passo: [docs/DEPLOY.md](docs/DEPLOY.md).

## Tecnologias

| Camada | Tecnologia |
| --- | --- |
| Frontend | React, Vite, TypeScript, CSS |
| HTTP no frontend | Axios |
| Backend | .NET 8, ASP.NET Core Web API, C# |
| Persistência | Entity Framework Core + PostgreSQL |
| API | REST + JSON |
| Documentação da API | Swagger / OpenAPI |
| Testes | xUnit |

## Requisitos

- .NET 8 SDK
- Node.js 18 ou superior
- PostgreSQL 16/17 em execução na porta `5432`

## Configuração do banco

Connection string padrão, em `backend/appsettings.json`:

```
Host=localhost;Port=5432;Database=cia;Username=postgres;Password=postgres
```

Se a senha local for diferente, altere `ConnectionStrings:DefaultConnection` ou defina a variável:

```
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=cia;Username=postgres;Password=SUA_SENHA
```

A API cria o banco `cia` na primeira execução, aplica a migration inicial e insere o cliente de demonstração.

## IA

A aplicação funciona sem chave de API externa, usando `LocalFallbackAiProvider`.

Para habilitar um provedor externo no futuro:

```
Ai__ApiKey=sua-chave
Ai__Endpoint=https://api.openai.com/v1/chat/completions
Ai__Model=gpt-4o-mini
```

Nunca coloque a chave no código-fonte.

## Execução

Abra dois terminais na raiz do repositório.

### 1. PostgreSQL

Confirme que o serviço do PostgreSQL está em execução.

### 2. Backend

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project backend/Cia.Api.csproj
```

A migration e o seed rodam automaticamente na inicialização.

URLs:

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger
- Health: http://localhost:5080/api/health

Resposta esperada do health check:

```json
{
  "status": "ok",
  "service": "CIA API"
}
```

### 3. Frontend

```bash
cd frontend
copy .env.example .env
npm install
npm run dev
```

No Linux/macOS use `cp .env.example .env`.

URL:

- http://localhost:5173

A variável `VITE_API_URL` aponta para `http://localhost:5080`.

## Cliente fictício de demonstração

| Campo | Valor |
| --- | --- |
| Customer ID | `CLIENTE-001` |
| Nome | Lucas |
| Telefone | 11999999999 |

Esses dados existem apenas para a demonstração acadêmica.

## Fluxo completo de demonstração

1. Abra http://localhost:5173 com o canal **App Claro**.
2. Envie: `Minha internet não está funcionando.`
3. A API cria a sessão, o protocolo e persiste as mensagens.
4. Envie: `Sim, já reiniciei o modem e continua sem internet.`
5. O contexto no PostgreSQL deve ficar com `IssueType = InternetConnection` e `ModemRestarted = true`.
6. Troque o canal para **WhatsApp**.
7. O protocolo permanece o mesmo.
8. Envie: `Quero continuar meu atendimento.`
9. A CIA recupera o contexto e menciona a internet e o modem já reiniciado.
10. Clique em **Falar com atendente** ou envie `Quero falar com um atendente.`
11. O status muda para `Transferred` e o resumo de transbordo é exibido.
12. Abra `/admin` e confira os indicadores e o histórico.

Para repetir a demonstração sem apagar o banco, use **Novo atendimento** na tela do chat após um transbordo. A próxima mensagem cria uma nova sessão ativa, com novo protocolo.

## Endpoints principais

- `GET /api/health`
- `GET /api/customers/{id}`
- `POST /api/sessions`
- `GET /api/sessions/{id}`
- `GET /api/sessions/customer/{customerId}`
- `POST /api/chat/message`
- `POST /api/sessions/{id}/channel`
- `POST /api/sessions/{id}/handoff`
- `GET /api/admin/dashboard`
- `GET /api/admin/sessions`
- `GET /api/admin/sessions/{id}`

## Migrations manuais

A API já aplica as migrations ao iniciar. Se quiser executar manualmente:

```bash
dotnet ef database update --project backend/Cia.Api.csproj
```

## Estrutura

```
/backend   API ASP.NET Core
/frontend  React + Vite
/tests     xUnit
/docs      Arquitetura, texto explicativo e roteiro do vídeo
```
