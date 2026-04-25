# Einsatzbereit

Volunteer coordination platform matching helpers with regional needs. German-language UI, English code/commits.

## Monorepo Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API        → backend/CLAUDE.md
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4  → frontend/CLAUDE.md
├── keycloak/       Custom Keycloak image + realm config  → keycloak/CLAUDE.md
├── docs/           arc42 architecture docs + ADRs        → docs/CLAUDE.md
└── .github/        CI/CD workflows + issue templates     → .github/CLAUDE.md
```

## Tech Stack (quick ref)

| | |
|---|---|
| Backend | .NET 10, EF Core 9, PostgreSQL 18 |
| Auth | Keycloak 26.6.1 (OIDC, JWT) |
| Frontend | Vite SPA, React 19, React Router v7, Tailwind CSS 4 |
| API client | NSwag-generated — **never hand-edit** `api-client.ts` |
| Tests (BE) | TUnit, Aspire.Hosting.Testing, Respawn, NetArchTest |
| Tests (FE) | E2E lives in backend `tests/VisualTests/` (TUnit.Playwright + Aspire) |
| CI/CD | GitHub Actions → GHCR |

## Development Setup

```bash
dotnet run --project backend/src/Aspire/AppHost
```

Aspire AppHost provisions Postgres, Keycloak, backend API, and the Vite frontend. URLs surface in the Aspire dashboard.

| Service | URL | Credentials |
|---|---|---|
| Frontend | http://localhost:4321 | — |
| Backend API | http://localhost:5000 | — |
| Keycloak admin | http://localhost:8080 | admin / admin |
| pgAdmin | http://localhost:5050 | admin@admin.com / admin |
| PostgreSQL | localhost:5432 | postgres / postgres |

Test users: `hannah/hannah123` (user), `olaf/olaf123` (user + organisator), `admin/admin123` (admin)

## Key Conventions

- Feature folders: `{Layer}/{Domain}/{Feature}/v1/` in both backend and frontend
- Routes: `/v{version:apiVersion}/...`, namespaces: `.v1`
- Commands/queries/DTOs: C# records
- Commits: Conventional Commits (`feat:`, `fix:`, `refactor:`, `chore:`, `test:`)
- No `.Result`/`.Wait()` — async all the way
