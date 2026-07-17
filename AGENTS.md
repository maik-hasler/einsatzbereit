# Einsatzbereit

Volunteer coordination platform matching helpers with regional needs. English UI and code, multilingual support.

## Monorepo Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API        → backend/AGENTS.md
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4  → frontend/AGENTS.md
├── keycloak/       Custom Keycloak image + realm config  → keycloak/AGENTS.md
├── docs/           arc42 architecture docs + ADRs        → docs/AGENTS.md
├── wiki/           Project LLM wiki (informal knowledge)  → wiki/AGENTS.md
└── .github/        CI/CD workflows + issue templates     → .github/AGENTS.md
```

## Tech Stack (quick ref)

| | |
|---|---|
| Backend | .NET 10 (SDK 10.0.300, see `backend/global.json`), EF Core 9, PostgreSQL 18 |
| Auth | Keycloak 26.6.4 (OIDC, JWT) |
| Frontend | Vite SPA, React 19, React Router v7, Tailwind CSS 4 |
| API client | NSwag-generated - **never hand-edit** `api-client.ts` |
| Tests (BE) | TUnit, Aspire.Hosting.Testing, Respawn, NetArchTest |
| Tests (FE) | E2E lives in backend `tests/VisualTests/` (TUnit.Playwright + Aspire) |
| CI/CD | GitHub Actions → GHCR |

## Development Setup

Required: .NET SDK **10.0.300** (enforced via `backend/global.json`).

```bash
dotnet run --project backend/src/Aspire/AppHost
```

Aspire AppHost provisions Postgres, Keycloak, backend API, and the Vite frontend. URLs surface in the Aspire dashboard.

| Service | URL | Credentials |
|---|---|---|
| Frontend | http://localhost:4321 | - |
| Backend API | http://localhost:5000 | - |
| Keycloak admin | http://localhost:8080 | admin / admin |
| pgAdmin | http://localhost:5050 | admin@admin.com / admin |
| PostgreSQL | localhost:5432 | postgres / postgres |
| Mailpit (email) | http://localhost:1080 | - (no auth required) |

Test users: `vera/vera123` (user), `olaf/olaf123` (user + organisator), `admin/admin123` (admin)

## Key Conventions

- Feature folders: `{Layer}/{Domain}/{Feature}/v1/` in both backend and frontend
- Routes: `/v{version:apiVersion}/...`, namespaces: `.v1`
- Commands/queries/DTOs: C# records
- Commits: Conventional Commits (`feat:`, `fix:`, `refactor:`, `chore:`, `test:`)
- No `.Result`/`.Wait()` - async all the way
- **Never use Unicode dashes** (U+2013 en dash, U+2014 em dash) in any file - write plain ASCII hyphens (`-`) instead; CI rejects non-ASCII dashes
- **Tab indentation is the default** (`.editorconfig`'s `[*]` rule) - shell scripts, AsciiDoc (`.adoc`), and PlantUML (`.puml`) all use tabs. Only `.md`, `.json`, and `.yml`/`.yaml` are overridden to spaces. CI's `editorconfig` job enforces this; when writing `.adoc` prose keep paragraphs on one unwrapped line rather than hand-wrapping with space-indented continuation lines

## Verify every bug fix / feature before calling it done

A fix or feature that hasn't been observed working live isn't done: cut a
release candidate, verify it against real staging (a health check plus an
end-to-end script exercising the changed behavior), add matching automated
regression coverage, and record the result in the PR description. See
`CLAUDE.md`'s "Mandatory: Deploy and verify" for the exact steps Claude Code
follows for this repo - adapt the same principle to your own tooling if
you're a different agent.
