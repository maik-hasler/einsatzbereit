# Contributing to Einsatzbereit

Thank you for your interest in contributing to Einsatzbereit!
Every contribution counts — bug reports, ideas, documentation, or code.

## Language Convention

| Context | Language |
|---|---|
| UI, end-user documentation | German |
| Code, commits, issues, pull requests | English |

## Getting Started

### Development Environment

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker](https://docs.docker.com/get-docker/), [pnpm](https://pnpm.io/installation).

```bash
git clone https://github.com/maik-hasler/einsatzbereit.git
cd einsatzbereit
dotnet run --project backend/src/Aspire/AppHost
```

All services start automatically. See [README.md](README.md) for service URLs and test users.

### Project Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4
├── keycloak/       Custom Keycloak image + realm config
├── postgres/       DB init script
├── docs/           arc42 architecture docs + ADRs
└── .github/        CI/CD workflows
```

Each component has its own `CLAUDE.md` with component-specific conventions.

## How to Contribute

### Reporting Bugs

Open an [Issue](../../issues/new) with:
- A short description of the problem
- Steps to reproduce
- Expected vs. actual behavior
- Environment info (OS, browser, Docker version)

### Feature Requests

Feature requests are welcome — open an Issue.
Describe the problem you want to solve, not just the desired solution.
This helps evaluate whether it aligns with the project's goals.

### Contributing Code

1. **Find or create an Issue** — confirm the change is wanted before writing code.
2. **Fork the repository** and create a feature branch:
   ```bash
   git checkout -b feat/short-description
   ```
3. **Make focused commits** — small, atomic commits preferred.
4. **Open a Pull Request** — describe what and why. Link the related Issue.
5. **Address review feedback** — reviews are part of the process.

### Branch Naming

| Type    | Pattern              | Example                    |
|---------|----------------------|----------------------------|
| Feature | `feat/description`   | `feat/opportunity-search`  |
| Bugfix  | `fix/description`    | `fix/date-parsing`         |
| Docs    | `docs/description`   | `docs/arc42-section-05`    |
| Chore   | `chore/description`  | `chore/update-deps`        |

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add volunteer matching endpoint
fix: correct date parsing in event filter
docs: update arc42 section 05 building block view
refactor: simplify opportunity query handler
test: add integration tests for auth flow
chore: update NuGet dependencies
```

Rules:
- Imperative mood, present tense: "add" not "added" or "adds"
- Max 72 characters in the subject line
- Reference the Issue number in the PR description, not the commit

## Pull Request Process

1. Keep PRs focused — one logical change per PR
2. Update documentation if your change affects behavior
3. Ensure CI passes before requesting review
4. The PR description should explain *why* the change is needed
5. Request review from `@maik-hasler`

## Testing

### Backend
```bash
cd backend
dotnet test
```

Integration tests use Testcontainers and require Docker.
Do not mock the database — all integration tests run against a real PostgreSQL instance.

### E2E

E2E tests live under `backend/tests/VisualTests/` (TUnit.Playwright + Aspire). They spin up the full stack via the Aspire AppHost — no separate dev server needed:

```bash
cd backend
dotnet test tests/VisualTests
```

### API Client

The frontend API client (`frontend/src/client/api-client.ts`) is **NSwag-generated**.
**Never hand-edit this file.** Regenerate it after backend changes:
```bash
# From the backend directory with the API running:
cd backend
dotnet tool run openapi
```

## Code Style

### Backend (C#)
- Follow Microsoft naming conventions (PascalCase for public members)
- Feature folders: `{Layer}/{Domain}/{Feature}/v1/`
- Commands/queries/DTOs as C# records
- Async all the way — no `.Result` or `.Wait()`

### Frontend (TypeScript)
- Named exports preferred over default exports
- Explicit return types on functions
- Feature folders mirroring the backend structure

### General
- Comments explain *why*, not *what*
- No dead code — remove, don't comment out
- Consistency within a module beats personal preference

## Architecture Decisions

Significant architectural decisions are documented as ADRs under `docs/ADRs/`.
If your contribution involves an architectural choice, propose a new ADR in your PR.

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).
By participating, you agree to uphold it.

## Questions?

If you're unsure where to start, open an Issue and ask.
No question is too small.
