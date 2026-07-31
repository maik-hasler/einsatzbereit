# .github - CI/CD & Issue Templates

## Workflows

```
.github/workflows/
├── dotnet.yml        Backend: build + test
├── frontend.yml      Frontend: lint → build
├── docs.yml          Docs: AsciiDoc build → GitHub Pages deploy
├── publish.yml       Tag-triggered: build + push backend/frontend/keycloak to GHCR, then deploy-staging
├── release-rc.yml    Promotes a release/v* branch into a real tag (used by Claude Code on the web)
├── reset-staging.yml Manual (workflow_dispatch): wipes staging Postgres + MinIO data, restarts with same images
├── lint.yml          Ban em/en dashes + EditorConfig check
└── pr-title.yml      Validate PR title against Conventional Commits
```

## CI Workflows (run on push/PR to main)

### `dotnet.yml`
- **Trigger:** `backend/**`, `frontend/**`, `keycloak/**` path filter or manual (frontend + keycloak included because VisualTests boots full stack via Aspire)
- **Jobs:** `build` (single `dotnet restore` + `dotnet build` for the whole `Einsatzbereit.slnx`, uploaded as the `backend-build` artifact via `actions/upload-artifact` with `include-hidden-files: true` - see below), then `format-check`, `fast-tests` (Application.UnitTests + ArchitectureTests), `integration-tests` (IntegrationTests, needs pnpm + Docker pre-pull for the Aspire stack), and `visual-tests` (VisualTests, same setup as `integration-tests`) all run in parallel with `needs: build`, each downloading that one artifact instead of restoring/building its own copy - `format-check` only strictly needs a restore, not a build, but is gated on `build` anyway so the whole workflow does exactly one restore, at the cost of its own signal landing later than it would standalone (behind `build`'s own setup+build time). Each restores the same `~/.nuget/packages` `actions/cache` key `build` populated (`--no-restore`/`--no-build` still need the actual package contents on disk for MSBuild to import analyzers/targets they contribute - confirmed locally: `dotnet run --no-restore --no-build` fails project evaluation with the NuGet cache missing, even with `bin`/`obj` present) then runs its test project(s) via `dotnet run --project ... --no-restore --no-build`. Every job downloading the artifact also runs a `chmod +x` pass over everything under `bin/` first - `actions/upload-artifact`/`download-artifact` zip the artifact and don't preserve the Unix executable bit, so the native apphost binaries (`bin/.../<ProjectName>`) came back non-executable on the first real run (an initial fix that only chmod'd extension-less filenames missed `Application.UnitTests`'s own binary, since that *project's* name itself contains a dot - dropped that filter, chmod'ing everything under `bin/` is harmless). `visual-tests` separately restores the `~/.cache/ms-playwright` `actions/cache` entry `build` populated while compiling `VisualTests.csproj` (the *browser* binaries - Chrome for Testing + Chrome Headless Shell, ~290 MiB combined, via `InstallPlaywrightBrowsers`'s `AfterTargets="Build"` MSBuild target - live outside the repo tree and don't travel in the artifact; `needs: build` guarantees this restore is a hit). The Playwright *driver* (a bundled Node runtime under `bin/.../.playwright/`, distinct from the browser) does travel in the artifact, but only because `include-hidden-files: true` is set - `upload-artifact` excludes dot-prefixed paths by default, which silently dropped that whole folder on the first real run and failed `visual-tests` with "Driver not found" (confirmed by downloading that run's actual artifact and listing its contents, not guessing). `publish.yml` mirrors this exact shape (`backend-build` -> `backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, no `backend-format-check` equivalent since `publish.yml` has no formatting gate). The consolidation is deliberate at the cost of a ~150-200 MiB compressed artifact transferred to every downstream job (measured against a real run) in exchange for one restore+build instead of four - see `docs/TDRs/2_slow_ci_pipeline.adoc` for the wall-clock trade-off this accepts and the full history of what broke getting here.
- **Test projects:** Application.UnitTests, ArchitectureTests, IntegrationTests, VisualTests
- **Why `dotnet run` not `dotnet test`:** TUnit uses Microsoft.Testing.Platform; `dotnet test` on .NET 10 requires opt-in to new experience. `dotnet run` invokes the test runner directly.
- **Typical duration:** previously ~10 minutes end to end (median ~9.6 min; grew from ~4.5 min in May 2026) with all four test projects running sequentially in one job, dominated by `VisualTests` (~57% of the run: boots the full Aspire stack, then drives it with Playwright). As of issue #773, that single job was split into three parallel jobs, and `VisualTests` itself no longer drives a real interactive Keycloak login for most of its 117 tests (`AuthHelper.FastSignInAsync` mints a token directly instead - see `docs/TDRs/2_slow_ci_pipeline.adoc`). First real run of that split (2026-07-19, n=1): `format-check` ~1 min, `fast-tests` ~1 min, `integration-tests` ~4 min, `visual-tests` ~6 min, all parallel - critical path ~6 min. The `build`-then-fan-out shape (2026-07-28) changes that math again: critical path is now `build` (~1-2 min) plus whichever of `fast-tests`/`integration-tests`/`visual-tests` takes longest, instead of each of those paying its own restore+build inside its own parallel timeline - a deliberate trade of a bit of wall-clock time for one restore+build instead of three (see the TDR for the reasoning and the ~200 MiB compressed artifact this costs per run). Not yet measured over a real run at the time of writing - treat both sets of numbers above as preliminary until re-measured (n=20+), same methodology as the TDR's original figure. When polling for this workflow's checks, don't re-poll more often than every ~2-3 minutes while it's in progress, and poll `build` plus all four downstream checks (`format-check`/`fast-tests`/`integration-tests`/`visual-tests`), since all four now gate on `build` finishing first.

### `frontend.yml`
- **Trigger:** `frontend/**` path filter or manual
- **Jobs (sequential):** lint → test → build
  - `lint`: `pnpm lint` + `pnpm check` (type check)
  - `test`: `pnpm test` (Vitest, `src/**/*.test.ts` - currently the `lib/` pure-function suite)
  - `build`: `pnpm build`
- **No E2E job** - E2E lives in backend `tests/VisualTests/` (run by `dotnet.yml`)

### `docs.yml`
- **Trigger:** `docs/**` path filter, push/PR to main, or manual
- **Jobs:** build AsciiDoc (`build`) -> deploy to GitHub Pages (`deploy`, `needs: build`)
- `deploy` is gated with `if: github.event_name == 'push'` so PRs only build and never deploy
- Uses `tonynv/asciidoctor-action` with `asciidoctor-diagram` for PlantUML
- `build` uses a per-ref concurrency group (`docs-build-${{ github.ref }}`) separate from `deploy`'s `pages` group, so a PR build can't cancel an in-progress production deployment

## Publish Workflows (tag-triggered)

All components share a single unified repo-level tag - see [VERSIONING.md](../VERSIONING.md).

| Tag pattern | Description |
|---|---|
| `vX.Y.Z` | Stable release |
| `vX.Y.Z-rc.N` | Release candidate |

Every component gets the same version tag:

| Component | Image name |
|---|---|
| Backend | `{repo}-backend` |
| Frontend | `{repo}-frontend` |
| Keycloak | `{repo}-keycloak` |

All images pushed to **GitHub Container Registry (GHCR)**.

**Release candidates:** `-rc.N` suffix (e.g., `v1.0.0-rc.1`). Image is published, tagged `staging` instead of `latest`, and `deploy-staging` runs.

**Full release:** Tag without `-rc` suffix → image published + `latest` tag updated.

### Publish flow (backend/frontend/keycloak)
1. Run full test suite - three parallel jobs (`backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, same split as `dotnet.yml`) that `publish-backend`, `publish-frontend`, and `publish-keycloak` all wait on via `needs:` before building anything, so a test failure blocks every image, not just the backend's. Frontend additionally runs its own lint/type-check/unit-tests/build inline; keycloak has no additional test gate beyond the shared backend suite
2. Login to GHCR
3. Extract version from tag (strips leading `v`)
4. Build and push Docker image
5. Tag with version + `latest` (if not RC) or `staging` (if RC)

## Cutting a release from Claude Code on the web

The Claude Code on the web git proxy restricts pushes to the current working branch only - tag refs cannot be pushed directly from the sandbox. To stay autonomous, use the `release-rc.yml` promotion workflow instead of asking the user to push a tag.

**The flow (Claude does this):**

```bash
# 1. From an up-to-date main, branch with the release name as the suffix.
git checkout -b release/v1.2.3-rc.1 main

# 2. Empty commit (or any commit on this branch) carries the push.
git commit --allow-empty -m "release: v1.2.3-rc.1"

# 3. Push the branch - sandbox allows this because it is the working branch.
git push -u origin release/v1.2.3-rc.1
```

`release-rc.yml` then:
1. Validates the suffix against `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`.
2. Creates an annotated tag with the same name on the branch's HEAD.
3. Pushes the tag using `RELEASE_TOKEN` (so `publish.yml` fires - see below).
4. Deletes the `release/...` branch.

After the tag exists, `publish.yml` runs end-to-end (build → GHCR → `deploy-staging` for RC tags).

**One-time setup the user must do:**

- Create a fine-grained Personal Access Token scoped to this repo with `contents: write`.
- Add it as a **repository secret** named `RELEASE_TOKEN`.

A PAT (not the default `GITHUB_TOKEN`) is mandatory because tags pushed with `GITHUB_TOKEN` do not trigger downstream workflows - GitHub explicitly prevents that to avoid workflow loops. Without `RELEASE_TOKEN`, `release-rc.yml` will fail at checkout.

**After pushing the branch:**

1. Poll the publish workflow's checks for the new tag (via `mcp__github__get_commit` → check_runs, or fetch `https://api.github.com/repos/{owner}/{repo}/commits/{sha}/check-runs`).
2. Once `deploy-staging` reports success, smoke-test live: `curl https://api.maik-hasler.de/health`, then HEAD-check `https://einsatzbereit.maik-hasler.de`.
3. If any publish job fails, diagnose from logs; if the deploy step itself fails, the SSH/GHCR secrets in the `staging` GitHub Environment are the most likely cause.

**Required `staging` Environment secret:** `KEYCLOAK_BACKEND_SECRET` - a randomly generated value (not a committed literal), used both as the `backend` Keycloak client's real secret (resolved into the `${KEYCLOAK_BACKEND_SECRET}` placeholder in `keycloak/realms/einsatzbereit-realm.json` at realm-import time) and as the backend app's `Keycloak__ClientSecret`. See `keycloak/AGENTS.md`. If unset, `deploy-staging` fails loudly (docker compose's `:?` guard in `docker-compose.yml`) rather than deploying with a weak default.

## Reset Workflow (manual)

`reset-staging.yml` wipes all staging test data - Postgres (`einsatzbereit` + `keycloak` databases, one instance) and MinIO uploads - then restarts the stack. It does **not** run `docker compose pull`, so the exact image tags/versions already running come back up unchanged; only data is reset.

- Trigger: `workflow_dispatch` only, with a required `confirm` input that must equal exactly `RESET`
- Runs in the `staging` GitHub Environment, reusing the same SSH secrets as `deploy-staging`
- Removes only the `postgres_data` and `minio_data` volumes - `postgres_backups` and `minio_backups` are left alone on purpose, as the recovery path if a reset needs to be walked back
- Because Keycloak's realm import runs with `OVERWRITE_EXISTING` (see `deploy-staging` in `publish.yml`) and the backend has `Database__MigrateOnStartup: true`, the stack re-migrates and re-imports `keycloak/realms/einsatzbereit-realm.json` on restart - the standard `vera`/`olaf`/`admin` test accounts come back automatically since they are defined in that checked-in realm config, not created ad hoc. This is by design: staging's demo credentials (including full-admin `admin`) are meant to be publicly known and instantly restorable, not rotated or hardened - see `keycloak/AGENTS.md` and #1166
- The backend also has `Database__SeedOnStartup` resolved from `DATABASE_SEED_ON_STARTUP` (see `docker-compose.yml`), which `deploy-staging` hardcodes to `"true"` directly in the rendered `.env` (not a secret - see `publish.yml`'s "Render .env" step) - defaults to `false` for any other deployment of the same compose file (#1375). On staging this means `ApplicationDbContextInitializer.SeedAsync()` runs on restart alongside the migrate/backfill calls, so staging comes back with the standard seeded organizations and published opportunities instead of an empty database. `SeedAsync()` is idempotent (no-ops if any `Organization` row already exists), so this is safe on every reset and on ordinary redeploys
- Ends with the same post-deploy health gate (`/health` polling) used by `deploy-staging`

## Issue Templates

```
.github/ISSUE_TEMPLATE/
├── bug_report.yml       [Bug]: prefix, label: bug
└── feature_request.yml  [Feature]: prefix, label: enhancement
```

Both templates are in **German**. Fields:

**Bug report:** Priorität (Niedrig/Mittel/Hoch), Beschreibung, Reproduktionsschritte, Zusätzliche Infos

**Feature request:** Priorität, User Story (Als X, möchte ich Y, damit Z), Akzeptanzkriterien (checkboxes), Beschreibung, Umsetzungsideen, Zusätzliche Infos

## Review & PR Template

- `.github/CODEOWNERS` - `* @maik-hasler`, so review is auto-requested on every PR instead of relying on contributors to remember (see `CONTRIBUTING.md` "Pull Request Process")
- `.github/PULL_REQUEST_TEMPLATE.md` - prompts for What/Why, the issue link, a Testing section, and the "Live verification" section required by root `AGENTS.md`'s deploy-and-verify flow

## Notes

- Path filters prevent unnecessary builds (backend change → only `dotnet.yml` runs)
- Keycloak shares the same unified tag as backend/frontend; the upstream Keycloak version is tracked separately as an OCI label (`org.opencontainers.image.base.version`), extracted from `keycloak/Dockerfile`'s `FROM` line
- GitHub Pages deployment uses `permissions: pages: write, id-token: write` with concurrency group to cancel stale deployments
