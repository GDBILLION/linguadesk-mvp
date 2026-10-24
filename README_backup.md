# LinguaDesk — Multilingual Customer Support MVP

A cinematic React + TypeScript support inbox backed by ASP.NET Core and PostgreSQL.

## Included
- React + Vite + TypeScript frontend with responsive dark, cinematic UI
- ASP.NET Core Web API (.NET 8) with EF Core / PostgreSQL
- Workspace, conversations, messages, translation preview, reply sending, usage endpoints
- Translation provider abstraction with a clearly marked demo translator and an optional LibreTranslate HTTP adapter
- Website chat widget API endpoints, Swagger/OpenAPI, Docker Compose
- Basic seed data and setup instructions

## Important MVP limitations
This is a runnable starter MVP, not a production-hardened SaaS. Authentication is intentionally a local development scaffold, customer widget sessions are basic, billing is represented by usage limits rather than a live payment provider, and the demo translator is not suitable for real customer conversations. Before production: add robust identity/OIDC, tenant-isolation security tests, signed widget sessions, anti-abuse controls, real billing/webhooks, durable background jobs, observability, data-retention controls, and a vetted translation provider.

## Requirements
- .NET 8 SDK
- Node.js 20+
- Docker Desktop (recommended) or PostgreSQL 15+

## Quick start with Docker
1. Copy `.env.example` to `.env`.
2. Run:
   ```bash
   docker compose up --build
   ```
3. Frontend: http://localhost:5173
4. API Swagger: http://localhost:5080/swagger
5. PostgreSQL: localhost:5432

The first API startup creates the schema and seeds demo conversations. Demo workspace ID is printed in API logs and is also `11111111-1111-1111-1111-111111111111`.

## Run without Docker

### Backend
```bash
cd backend/LinguaDesk.Api
dotnet restore
dotnet run
```
Configure `ConnectionStrings__Default` with your PostgreSQL connection string. API defaults to `http://localhost:5080`.

### Frontend
```bash
cd frontend
npm install
npm run dev
```
Set `VITE_API_BASE_URL=http://localhost:5080` in `frontend/.env.local` if needed.

## Translation provider
Default mode is `demo`, which returns illustrative translations for a small set of common phrases and visibly marks other output as demo output. It is useful only for UI development.

For real translations, set:
- `Translation__Provider=libretranslate`
- `Translation__LibreTranslateUrl=https://your-approved-instance/translate`
- `Translation__ApiKey=...` (optional, depending on provider)

The adapter expects a LibreTranslate-compatible `/translate` endpoint. Confirm the endpoint, security, language support, retention terms, and pricing with your chosen provider.

## Main API routes
- `GET /api/v1/dashboard`
- `GET /api/v1/conversations`
- `GET /api/v1/conversations/{id}`
- `GET /api/v1/conversations/{id}/messages`
- `POST /api/v1/translations/preview`
- `POST /api/v1/conversations/{id}/messages`
- `PATCH /api/v1/conversations/{id}/status`
- `POST /api/v1/public/chat/{workspacePublicId}/conversations`
- `POST /api/v1/public/chat/{workspacePublicId}/conversations/{conversationId}/messages`
- `GET /api/v1/billing/usage`

## Security note
The demo API uses a fixed demo workspace and is not intended to expose publicly. Do not deploy it with customer data until authentication and tenant authorization are implemented and independently tested.
