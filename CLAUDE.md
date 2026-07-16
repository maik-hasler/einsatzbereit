# Einsatzbereit

Volunteer coordination platform matching helpers with regional needs. English UI and code, multilingual support.

## Monorepo Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API        → backend/CLAUDE.md
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4  → frontend/CLAUDE.md
├── keycloak/       Custom Keycloak image + realm config  → keycloak/CLAUDE.md
├── docs/           arc42 architecture docs + ADRs        → docs/CLAUDE.md
├── wiki/           Project LLM wiki (informal knowledge)  → wiki/CLAUDE.md
└── .github/        CI/CD workflows + issue templates     → .github/CLAUDE.md
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

Required: .NET SDK **10.0.300** (enforced via `backend/global.json`). In Claude Code web/cloud sessions, the `SessionStart` hook installs it automatically via `dotnet-install.sh` if `dotnet` is not already on `PATH`.

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

## Sandbox Limitations (Claude Code on the web)

- **No reliable Docker** - `dotnet run --project backend/src/Aspire/AppHost`, the `IntegrationTests` project (Testcontainers), and the `VisualTests` project (Aspire + Playwright) all need real container networking. Don't try to run them locally in a web/cloud session, even if `docker info` succeeds - Aspire/DCP orchestration still fails. Verify locally with `dotnet build` + `Application.UnitTests` + `ArchitectureTests` (no Docker needed); CI's `dotnet.yml` runs the full suite including `IntegrationTests`/`VisualTests` on a real runner. For anything user-visible, use the release-candidate + live-staging Playwright flow below instead of a local dev server.
- **Direct pushes to `main` are blocked** by the git proxy (working-branch only) - always commit to the designated `claude/...` branch and open a PR, even if an instruction says to work "directly on main".

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
  diff touches, required before opening a PR (see below). `.claude/skills/issue-triage/`
  is the recurring triage-and-implement loop for this repo's autonomous
  routine - the durable process lives here, checked in and versioned,
  rather than only in the routine's own (unowned, unversioned) prompt text.
  `.claude/skills/persona-simulation/` is that routine's fallback for a
  genuinely empty backlog - it drives the live app as Volunteer Vera/Organizer
  Olaf to find real gaps in the existing feature set, filing GitHub issues
  only (never code) and labelling anything needing the repo owner's own
  product call as `needs-decision`, which `issue-triage` then leaves alone.
- **Hooks** - `.claude/hooks/protect-generated-clients.sh` blocks Edit/Write
  on the three NSwag-generated files (see "API client" row above).
  `.claude/hooks/pre-stop-verify.sh` (`Stop` hook) runs `dotnet build`/`pnpm lint`+`check`
  once before ending a turn if backend/frontend source changed, blocking
  only on an actual failure (capped at 2 blocks per session so it fails
  open rather than risk a loop) - a safety net since this routine has no
  human review before a PR goes out.
- **Plugins** - the `dotnet/skills` marketplace (`dotnet-aspnetcore`,
  `dotnet-test`, `dotnet-nuget`, `dotnet-data`) plus the official
  `csharp-lsp`, `typescript-lsp`, and `playwright` (Microsoft's Playwright
  MCP - live browser control for ad-hoc exploration/debugging during live
  verification) plugins are enabled in `.claude/settings.json`. The
  `playwright` plugin is for interactive poking around, not a replacement
  for the persisted smoke-test script required below - that stays as the
  reviewable, committed record of what was verified.

## Releases (autonomous from Claude Code on the web)

Releases are driven by tags. The Claude Code on the web git proxy blocks tag pushes (working-branch only), so **do not** ask the user to `git push` a tag - push a `release/vX.Y.Z[-rc.N]` branch instead and let `.github/workflows/release-rc.yml` promote it. Full flow + the one-time `RELEASE_TOKEN` setup are documented in `.github/CLAUDE.md` under "Cutting a release from Claude Code on the web".

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
6. Once `deploy-staging` reports success, smoke-test with Playwright against the live site:
   - `curl -sf https://api.maik-hasler.de/health` - must return HTTP 200
   - Run (or write + run) a **manual Playwright script** in `scripts/` that exercises the changed behaviour end-to-end against `https://einsatzbereit.maik-hasler.de`. The script must exit 0 (all assertions green).
     ```bash
     # Install playwright once per session if needed. The root package.json
     # already pins the version - use bare `npm install`, never
     # `npm install --save-dev playwright` (that bumps the pin to a caret
     # range and dirties package-lock.json for no reason).
     npm install && npx playwright install chromium
     # Run the smoke script for the feature you just fixed
     node scripts/smoke-test-<feature>.mjs
     ```
   - Notes on live Playwright scripts:
     - **Use `scripts/lib/live-browser.mjs`** (`launchLiveBrowser()`, `loginKeycloak()`) instead of copy-pasting a new browser launch / login sequence - it already has `ignoreHTTPSErrors: true` and the sandbox's egress-proxy workaround baked in (the proxy re-terminates TLS, and Chromium's default ClientHello doesn't survive that without pinned launch args). Most pre-existing scripts in `scripts/` predate this helper and launch plain `chromium.launch()` - don't copy one of those as a template, import the helper instead.
     - The sign-in button text may be "Sign in" or "Anmelden" - use `/sign in|anmelden/i` for the button that navigates to Keycloak, then call `loginKeycloak(page, username, password)` once there.
7. Add the same assertions as an **automated C# TUnit test** in `backend/tests/VisualTests/` (runs against the local Aspire stack in CI). The local Keycloak uses a single-step login - `AuthHelper.LoginAsync` handles this.
8. Document the result (pass/fail + what was observed) in the PR description under a **"Live verification"** section.
9. Only then mark the task complete.
