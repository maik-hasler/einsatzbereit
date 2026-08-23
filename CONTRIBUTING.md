# Contributing to Einsatzbereit

Thank you for your interest in contributing to Einsatzbereit!
Every contribution counts - bug reports, ideas, documentation, or code.

## Language Convention

| Context | Language |
|---|---|
| UI source strings (i18n keys in `frontend/src/locales/en.json`) | English - add or edit new keys here first |
| UI German translation (`frontend/src/locales/de.json`) | German - keep in parity with `en.json` via `pnpm i18n:check` |
| End-user-facing app and documentation | German (Einsatzbereit's primary audience is German-speaking; the UI negotiates the visitor's browser language at runtime and falls back to German - `<html lang="de">` - only when that can't be detected, see `frontend/src/i18n.ts`) |
| Installed-app metadata (the web app manifest in `frontend/vite.config.ts`) | Localized per active i18next language - a separate `manifest.de.webmanifest`/`manifest.en.webmanifest` is built at build time (`deManifest`/`enManifest`), and `frontend/src/i18n.ts` swaps `index.html`'s `<link rel="manifest">` between them as the visitor's language changes. German remains the default (`manifest.de.webmanifest`) for a visitor whose language hasn't resolved yet |
| Code, commits, issues, pull requests | English |

A PR that adds or changes UI text must update both `en.json` (the source) and `de.json` (the translation) - see `frontend/AGENTS.md`.

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

See [README.md](README.md#project-structure) for the top-level directory layout.

Each component has its own `AGENTS.md` with component-specific conventions
(Claude Code additionally reads a same-named `CLAUDE.md`, which imports
`AGENTS.md` and can also hold Claude-Code-only notes).

## How to Contribute

### Reporting Bugs

Open an [Issue](../../issues/new) with:
- A short description of the problem
- Steps to reproduce
- Expected vs. actual behavior
- Environment info (OS, browser, Docker version)

### Feature Requests

Feature requests are welcome - open an Issue.
Describe the problem you want to solve, not just the desired solution.
This helps evaluate whether it aligns with the project's goals.

### Contributing Code

1. **Find or create an Issue** - confirm the change is wanted before writing code.
2. **Fork the repository** and create a feature branch:
   ```bash
   git checkout -b feat/short-description
   ```
3. **Make focused commits** - small, atomic commits preferred.
4. **Open a Pull Request** - describe what and why. Link the related Issue.
5. **Address review feedback** - reviews are part of the process.

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

PR titles are validated against Conventional Commits in CI by the [`PR Title`](.github/workflows/pr-title.yml) workflow. Non-conforming titles fail the check.

## Pull Request Process

1. Keep PRs focused - one logical change per PR
2. Update documentation if your change affects behavior
3. Ensure CI passes before requesting review
4. Fill in the PR template (`.github/PULL_REQUEST_TEMPLATE.md`) - it explains *why* the change is needed and links the issue
5. Review is auto-requested via [`.github/CODEOWNERS`](.github/CODEOWNERS) (`@maik-hasler`)

## Testing

### Backend

TUnit uses Microsoft.Testing.Platform, so run each test project with `dotnet run --project <path>`, not `dotnet test` (which needs opt-in to the new testing experience on .NET 10 and isn't how CI runs these - see `.github/workflows/dotnet.yml`):

```bash
cd backend
dotnet run --project tests/Application.UnitTests
dotnet run --project tests/ArchitectureTests
```

Integration tests boot the real Aspire AppHost (`Aspire.Hosting.Testing`) and require Docker:
```bash
dotnet run --project tests/IntegrationTests
```
Do not mock the database - all integration tests run against a real PostgreSQL instance.

### E2E

E2E tests live under `backend/tests/VisualTests/` (TUnit.Playwright + Aspire). They spin up the full stack via the Aspire AppHost - no separate dev server needed:

```bash
cd backend
dotnet run --project tests/VisualTests
```

### API Client

The frontend API client (`frontend/src/client/api-client.ts`) is **NSwag-generated**.
**Never hand-edit this file.** It regenerates automatically the next time the backend builds in the Debug configuration (the `NSwag` MSBuild target in `src/Api/Api.csproj`, gated on `Configuration == Debug`):
```bash
cd backend
dotnet build
```

## Code Style

### Backend (C#)
- Follow Microsoft naming conventions (PascalCase for public members)
- Feature folders: `{Layer}/{Domain}/{Feature}/v1/`
- Commands/queries/DTOs as C# records
- Async all the way - no `.Result` or `.Wait()`

### Frontend (TypeScript)
- Named exports preferred over default exports
- Explicit return types on functions
- Feature folders mirroring the backend structure

### General
- Comments explain *why*, not *what*
- No dead code - remove, don't comment out
- Consistency within a module beats personal preference

The code speaks for itself. A comment is the exception, not the habit, and it
has to earn its place by warning about something the code cannot show: a trap
where the obvious edit silently breaks correctness or security. Ordering that
must hold, a guard whose removal opens a race or an authorization hole, a
literal that looks redundant but is load-bearing, two files that must change
together. If a reader would be safe not knowing it, leave it out.

Everything else goes in the name, the type, or the test. Design rationale,
history, issue archaeology and restatements of the line below are not comments,
they are noise - the issue tracker and `git log` already hold them, and unlike a
comment they cannot drift out of date in place.

The same bar applies everywhere: production code, tests, CI workflows, shell
scripts and themes. Tests get no narration at all - no explanation above a class
or a method, none inside a phase. `// Arrange` / `// Act` / `// Assert` stay as
bare markers, and carry nothing else; what a test proves belongs in its method
name and its assertions.

Three kinds of comment look like prose but are not, and must survive:
directives the toolchain reads (`eslint-disable-*`, `/// <reference ... />`,
`# v1.2.3` on a pinned action SHA); a comment that is a block's only content,
which is what stops ESLint's `no-empty` firing on a deliberately empty `catch`;
and the HTML comments in `.github/PULL_REQUEST_TEMPLATE.md` and the issue
templates, which are the prompts a contributor fills in and never render.

`scripts/comment-density.py` reports the ratio of comment lines to non-blank
lines across hand-written sources - code, CI workflows, shell scripts, themes
and docs alike (`--top N` ranks the densest files). It is a diagnostic, not a
gate: at this bar a file drifting upward is usually one that started explaining
itself again.

## Dependency Management

Dependencies are managed by [Renovate](https://docs.renovatebot.com/) (config: `renovate.json`).

The default `rangeStrategy` is `pin` - any new dependency range gets pinned to an exact version (e.g. NuGet `[10.0.7]`, npm `19.2.5`). The one exception is the `msbuild-sdk` manager: SDK references like `Aspire.AppHost.Sdk` must stay floating because bracketed pins (`[13.2.4]`) break SDK resolution. Two `packageRules` enforce this - one sets `rangeStrategy: replace` for all msbuild-sdk updates, the other disables `pin` update PRs entirely. Both rules must stay separate; merging them would only apply `replace` to pin updates and let the global `pin` strategy leak into regular version bumps.

## Architecture Decisions

Significant architectural decisions are documented as ADRs under `docs/ADRs/`.
If your contribution involves an architectural choice, propose a new ADR in your PR.

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).
By participating, you agree to uphold it.

## Questions?

Unsure where to start? Open an Issue - see "Reporting Bugs" above.
