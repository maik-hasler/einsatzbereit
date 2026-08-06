# Einsatzbereit

Volunteer coordination platform matching helpers with regional needs. English-source UI strings and code; German is the default served locale. See `CONTRIBUTING.md`'s Language Convention for the full breakdown.

## Monorepo Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API        → backend/AGENTS.md
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4  → frontend/AGENTS.md
├── keycloak/       Custom Keycloak image + realm config  → keycloak/AGENTS.md
├── docs/           arc42 architecture docs + ADRs        → docs/AGENTS.md
└── .github/        CI/CD workflows + issue templates     → .github/AGENTS.md
```

## Tech Stack (quick ref)

| | |
|---|---|
| Backend | .NET 10 (SDK 10.0.302, see `backend/global.json`), EF Core 10, PostgreSQL 18 |
| Auth | Keycloak 26.7.0 (OIDC, JWT) |
| Frontend | Vite SPA, React 19, React Router v8, Tailwind CSS 4 |
| API client | NSwag-generated - **never hand-edit** `api-client.ts` |
| Tests (BE) | TUnit, Aspire.Hosting.Testing, Respawn, NetArchTest |
| Tests (FE) | E2E lives in backend `tests/VisualTests/` (TUnit.Playwright + Aspire) |
| CI/CD | GitHub Actions → GHCR |

## Development Setup

Required: .NET SDK **10.0.302** (enforced via `backend/global.json`).

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

These same test-user credentials are intentionally also live on the public staging deployment (`https://einsatzbereit.maik-hasler.de`) - staging bakes in the same Keycloak realm as local dev, on purpose, since staging is disposable demo/QA infrastructure rather than production (see the `README.md` Test Users note and `keycloak/AGENTS.md` for the full rationale). Full admin access via these credentials on staging is a known, accepted trade-off, not a vulnerability to report.

## Key Conventions

- Feature folders: `{Layer}/{Domain}/{Feature}/v1/` in both backend and frontend
- Routes: `/v{version:apiVersion}/...`, namespaces: `.v1`
- Commands/queries/DTOs: C# records
- Commits: Conventional Commits (`feat:`, `fix:`, `refactor:`, `chore:`, `test:`)
- No `.Result`/`.Wait()` - async all the way
- **Never use Unicode dashes** (U+2013 en dash, U+2014 em dash) in any file - write plain ASCII hyphens (`-`) instead; CI rejects non-ASCII dashes
- **Tab indentation is the default** (`.editorconfig`'s `[*]` rule) - shell scripts, AsciiDoc (`.adoc`), and PlantUML (`.puml`) all use tabs. Only `.md`, `.json`, `.yml`/`.yaml`, and `.py` (PEP 8) are overridden to spaces. CI's `editorconfig` job enforces this; when writing `.adoc` prose keep paragraphs on one unwrapped line rather than hand-wrapping with space-indented continuation lines

## Claude Code Configuration

`.claude/` holds the self-review setup for this repo (all report-only, never
edit on your own initiative):

- **Agents** (`.claude/agents/`) - proactive subagents invoked automatically
  when their trigger condition matches, no explicit request needed:
  `nswag-check` (endpoint/DTO changes vs. generated clients),
  `ef-migration-check` (entity changes vs. EF Core migrations),
  `architecture-check` (Clean Architecture layer/naming/rate-limiting rules),
  `a11y-check` (frontend components vs. the a11y conventions below - scoped
  to only what ESLint's `jsx-a11y` ruleset can't already catch, see the
  agent file for why), `i18n-check` (`en.json`/`de.json` translation key
  parity - nothing else in CI checks this).
- **Skills** - `.claude/skills/self-review/` (`/self-review`) runs a
  prioritised diff review and fans out to the agents above for the areas the
  diff touches, required before opening a PR (see below).
  `.claude/skills/lens/` is this repo's autonomous routine and on-demand
  review tool: one lens per run - static repo audits (bugs, dead code, dead
  features, repo hygiene, docs drift, test gaps, CI, security, contributor
  accessibility) or live passes against staging as Vera/Olaf/Admin
  (personas, accessibility), code/comment complexity, or comment bloat -
  chosen by triage or named by the user. Report-only: files GitHub issues
  (label `lens`, capped at 5/run), never code or a PR.
  `.claude/skills/frontend-design/` (vendored from `anthropics/skills`,
  Apache-2.0, `LICENSE` alongside it) pushes frontend redesign work
  toward a deliberate, non-generic visual direction - typography, color
  theming, motion, spatial composition - instead of generic AI-layout
  defaults; load it before visual/layout changes to frontend components
  or pages. `.claude/skills/live-verify/` (`/live-verify`) is step 6 of
  "Mandatory: Deploy and verify" below - the throwaway live-staging
  Playwright recipe (TLS launch args, Keycloak login) lives there, not
  inlined in this file.
- **Hooks** - `.claude/hooks/protect-generated-clients.sh` blocks Edit/Write
  on the three NSwag-generated files (see "API client" row above).
  `.claude/hooks/pre-stop-verify.sh` (`Stop` hook) runs `dotnet build`/`pnpm lint`+`check`
  once before ending a turn if backend/frontend source changed, blocking
  only on an actual failure (capped at 2 blocks per session so it fails
  open rather than risk a loop) - a safety net since this routine has no
  human review before a PR goes out. The `SessionStart` hook installs the
  .NET SDK version pinned in `backend/global.json` automatically via
  `dotnet-install.sh` in Claude Code web/cloud sessions if `dotnet` is not
  already on `PATH` (see this file's Development Setup for the SDK
  requirement itself).
- **Plugins** - the `dotnet/skills` marketplace (`dotnet-aspnetcore`,
  `dotnet-test`, `dotnet-nuget`, `dotnet-data`) plus `csharp-lsp`,
  `typescript-lsp`, and `playwright` (live browser control) are enabled in
  `.claude/settings.json`. `playwright` is for interactive poking around,
  not a replacement for the live-verification script required below.
  **MCP tool grants don't propagate to an `Agent`-tool subagent** - drive
  live browser sessions (a `lens` live pass, a design review) in the
  current session directly, never delegate them. Availability can also
  vary turn-to-turn even in the main session - `ToolSearch` for
  `browser_navigate` first; if nothing resolves, fall back to the
  `/live-verify` skill's scratch-script recipe.

## Sandbox Limitations (Claude Code on the web)

- **No reliable Docker** - `dotnet run --project backend/src/Aspire/AppHost`, the `IntegrationTests` project (Testcontainers), and the `VisualTests` project (Aspire + Playwright) all need real container networking. Don't try to run them locally in a web/cloud session, even if `docker info` succeeds - Aspire/DCP orchestration still fails. Verify locally with `dotnet build` + `Application.UnitTests` + `ArchitectureTests` (no Docker needed); CI's `dotnet.yml` runs the full suite including `IntegrationTests`/`VisualTests` on a real runner. For anything user-visible, use the release-candidate + live-staging Playwright flow below instead of a local dev server.
- **Direct pushes to `main` are blocked** by the git proxy (working-branch only) - always commit to the designated `claude/...` branch and open a PR, even if an instruction says to work "directly on main".
- **This repo ships fast** - `git fetch origin main` and skim recent commits before any review/analysis task, not just implementation work; assuming last week's state is current wastes most of a review's effort re-finding what already shipped.

## Releases (autonomous from Claude Code on the web)

Releases are driven by tags. The Claude Code on the web git proxy blocks tag pushes (working-branch only), so **do not** ask the user to `git push` a tag - push a `release/vX.Y.Z[-rc.N]` branch instead and let `.github/workflows/release-rc.yml` promote it. Full flow + the one-time `RELEASE_TOKEN` setup are documented in `.github/AGENTS.md` under "Cutting a release from Claude Code on the web".

## Mandatory: Deploy and verify every bug fix / feature

After every bug fix or feature implementation, **always** cut a release candidate and verify changes on the live staging environment before closing out the task. This is not optional - a fix that has not been observed working in production is not done.

**Steps (must be followed in order):**

1. Self-review the diff (`/self-review`) and fix anything it flags before opening the PR.
2. Confirm all CI checks on the PR are green.
3. Determine the next RC version: check existing tags (`mcp__github__list_tags`) and increment the RC counter (e.g. `v1.0.0-rc.8` -> `v1.0.0-rc.9`).
4. Create the release branch **from the feature branch** (so the fix is included):
   ```bash
   git checkout -b release/vX.Y.Z-rc.N <feature-branch>
   git commit --allow-empty -m "release: vX.Y.Z-rc.N"
   git push -u origin release/vX.Y.Z-rc.N
   ```
5. `release-rc.yml` creates the tag; `publish.yml` builds images and runs `deploy-staging`. Monitor via `mcp__github__pull_request_read get_check_runs` on the release commit, or poll the Actions tab.
6. Once `deploy-staging` reports success, run the **`/live-verify`** skill: it checks the health endpoint, then writes and runs a throwaway Playwright script in a scratch directory (never `scripts/` - there is no committed `scripts/` directory or root `package.json` anymore) against `https://einsatzbereit.maik-hasler.de`. Must exit 0 (all assertions green), then get deleted.
7. Add the same assertions as an **automated C# TUnit test** in `backend/tests/VisualTests/` (runs against the local Aspire stack in CI). The local Keycloak uses a single-step login - `AuthHelper.LoginAsync` handles this. This is the durable, reviewable record of the fix; the scratch script from step 6 is not - it gets deleted once it has served its purpose.
8. Document the result (pass/fail + what was observed) in the PR description under a **"Live verification"** section.
9. Only then mark the task complete.

Live staging accumulates test debris over time from the shared `vera`/`olaf`/`admin` accounts - prefer scripts that clean up after themselves. `.github/workflows/reset-staging.yml` (manual, destructive confirmation gate) wipes and reseeds staging when it gets bad enough - know it exists rather than working around dirty data by hand, but don't trigger it without the repo owner's go-ahead.
