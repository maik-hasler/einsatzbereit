# Einsatzbereit

Volunteer coordination platform matching helpers with regional needs. English UI and code, multilingual support.

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
| Backend | .NET 10 (SDK 10.0.300, see `backend/global.json`), EF Core 9, PostgreSQL 18 |
| Auth | Keycloak 26.6.2 (OIDC, JWT) |
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
- **Shell scripts use tab indentation** - the EditorConfig rule for `.sh` files requires tabs, not spaces

## Releases (autonomous from Claude Code on the web)

Releases are driven by tags. The Claude Code on the web git proxy blocks tag pushes (working-branch only), so **do not** ask the user to `git push` a tag - push a `release/vX.Y.Z[-rc.N]` branch instead and let `.github/workflows/release-rc.yml` promote it. Full flow + the one-time `RELEASE_TOKEN` setup are documented in `.github/CLAUDE.md` under "Cutting a release from Claude Code on the web".

## Mandatory: Deploy and verify every bug fix / feature

After every bug fix or feature implementation, **always** cut a release candidate and verify changes on the live staging environment before closing out the task. This is not optional - a fix that has not been observed working in production is not done.

**Steps (must be followed in order):**

1. Confirm all CI checks on the PR are green.
2. Determine the next RC version: check existing tags (`mcp__github__list_tags`) and increment the RC counter (e.g. `v1.0.0-rc.8` -> `v1.0.0-rc.9`).
3. Create the release branch **from the feature branch** (so the fix is included):
   ```bash
   git checkout -b release/vX.Y.Z-rc.N <feature-branch>
   git commit --allow-empty -m "release: vX.Y.Z-rc.N"
   git push -u origin release/vX.Y.Z-rc.N
   ```
4. `release-rc.yml` creates the tag; `publish.yml` builds images and runs `deploy-staging`. Monitor via `mcp__github__pull_request_read get_check_runs` on the release commit, or poll the Actions tab.
5. Once `deploy-staging` reports success, smoke-test with Playwright against the live site:
   - `curl -sf https://api.maik-hasler.de/health` - must return HTTP 200
   - Run (or write + run) a **manual Playwright script** in `scripts/` that exercises the changed behaviour end-to-end against `https://einsatzbereit.maik-hasler.de`. The script must exit 0 (all assertions green).
     ```bash
     # Install playwright once per session if needed
     npm install --save-dev playwright && npx playwright install chromium
     # Run the smoke script for the feature you just fixed
     node scripts/smoke-test-<feature>.mjs
     ```
   - Notes on live Playwright scripts:
     - Use `ignoreHTTPSErrors: true` in `browser.newContext()` (sandbox TLS)
     - The live Keycloak (`login.maik-hasler.de`) uses a **two-step** login: fill `#username` -> click `#kc-login` -> fill `#password` -> click `#kc-login`
     - The sign-in button text may be "Sign in" or "Anmelden" - use `/sign in|anmelden/i`
6. Add the same assertions as an **automated C# TUnit test** in `backend/tests/VisualTests/` (runs against the local Aspire stack in CI). The local Keycloak uses a single-step login - `AuthHelper.LoginAsync` handles this.
7. Document the result (pass/fail + what was observed) in the PR description under a **"Live verification"** section.
8. Only then mark the task complete.
