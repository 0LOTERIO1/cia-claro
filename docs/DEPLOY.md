# Publicar a CIA na web

A Vercel hospeda bem o **frontend React**. Ela **não executa** a API ASP.NET Core .NET 8 nem o PostgreSQL.

Para a aplicação ficar disponível na internet, o arranjo correto é:

```
Navegador
  → Frontend na Vercel
    → API ASP.NET Core em outro host (Render, Railway, Azure, etc.)
      → PostgreSQL na nuvem
```

Sem uma API pública, o site na Vercel abre, mas o chat continua mostrando que o servidor está offline.

## 1. Frontend na Vercel

1. Envie o repositório para o GitHub.
2. Em [vercel.com](https://vercel.com), importe o projeto.
3. Configure:
   - **Root Directory:** `frontend`
   - **Framework Preset:** Vite
   - **Build Command:** `npm run build`
   - **Output Directory:** `dist`
4. Crie a variável de ambiente:

```
VITE_API_URL=https://SUA-API-PUBLICA
```

Use a URL real da API, sem barra no final. Exemplo: `https://cia-api.onrender.com`

5. Faça o deploy.

A rota `/admin` funciona porque `frontend/vercel.json` redireciona o React Router para `index.html`.

## 2. API e banco fora da Vercel

Publique o backend em um serviço que rode .NET 8, por exemplo [Render](https://render.com):

- tipo **Web Service**
- runtime **Docker** ou build `dotnet publish`
- comando de start: `dotnet Cia.Api.dll`
- PostgreSQL gerenciado no mesmo provedor (ou Neon, Supabase, etc.)

Variáveis da API:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=cia;Username=...;Password=...;SSL Mode=Require
Cors__AllowedOrigins__0=https://seu-projeto.vercel.app
```

O CORS já libera origens `https://*.vercel.app` e `localhost`. Ainda assim, cadastre a URL exata da Vercel.

A API aplica a migration e o seed do cliente `CLIENTE-001` na inicialização.

## 3. Conferência

1. `GET https://SUA-API/api/health` deve responder `{ "status": "ok", "service": "CIA API" }`.
2. Abra a URL da Vercel.
3. O cliente Lucas deve carregar e o chat deve responder.

## O que não fazer

Não publique só o React na Vercel apontando `VITE_API_URL` para `http://localhost:5080`. Isso funciona apenas na sua máquina.
