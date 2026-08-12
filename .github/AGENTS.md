# .github - CI/CD & Issue Templates

## Workflows

```
.github/workflows/
├── dotnet.yml                  Backend: build + test
├── frontend.yml                Frontend: calls frontend-checks.yml (test-domain values)
├── frontend-checks.yml         Reusable (workflow_call): lint → test/build/production-container-smoke-test - shared by frontend.yml and publish.yml so the release path can't skip checks the PR gate runs (#1733)
├── docs.yml                    Docs: AsciiDoc build (push + PR) → GitHub Pages deploy (push only)
├── keycloak-realm-import.yml   Verifies the committed realm still imports on the production Keycloak version
├── publish.yml                 Tag-triggered: build + push backend/frontend/keycloak to GHCR, create a GitHub Release, then deploy-staging (RC tags) or deploy-production (stable tags)
├── release-rc.yml              Promotes a release/v* branch into a real tag (used by Claude Code on the web)
├── reset-staging.yml           Manual (workflow_dispatch): wipes staging Postgres + MinIO data, restarts with same images
├── lint.yml                    Ban em/en dashes + EditorConfig check
└── pr-title.yml                Validate PR title against Conventional Commits
```

## CI Workflows (run on push/PR to main)

### `dotnet.yml`
- **Trigger:** `backend/**`, `frontend/**`, `keycloak/**` path filter or manual (frontend + keycloak included because VisualTests boots full stack via Aspire)
- **Jobs:** `build` (single `dotnet restore` + `dotnet build` for the whole `Einsatzbereit.slnx`, uploaded as the `backend-build` artifact via `actions/upload-artifact` with `include-hidden-files: true` - see below), then `format-check`, `fast-tests` (Application.UnitTests + ArchitectureTests), `integration-tests` (IntegrationTests, needs pnpm + Docker pre-pull for the Aspire stack), and `visual-tests` (VisualTests, same setup as `integration-tests`) all run in parallel with `needs: build`, each downloading that one artifact instead of restoring/building its own copy - `format-check` only strictly needs a restore, not a build, but is gated on `build` anyway so the whole workflow does exactly one restore, at the cost of its own signal landing later than it would standalone (behind `build`'s own setup+build time). Each restores the same `~/.nuget/packages` `actions/cache` key `build` populated (`--no-restore`/`--no-build` still need the actual package contents on disk for MSBuild to import analyzers/targets they contribute - confirmed locally: `dotnet run --no-restore --no-build` fails project evaluation with the NuGet cache missing, even with `bin`/`obj` present) then runs its test project(s) via `dotnet run --project ... --no-restore --no-build`. Every job downloading the artifact also runs a `chmod +x` pass over everything under `bin/` first - `actions/upload-artifact`/`download-artifact` zip the artifact and don't preserve the Unix executable bit, so the native apphost binaries (`bin/.../<ProjectName>`) came back non-executable on the first real run (an initial fix that only chmod'd extension-less filenames missed `Application.UnitTests`'s own binary, since that *project's* name itself contains a dot - dropped that filter, chmod'ing everything under `bin/` is harmless). `visual-tests` separately restores the `~/.cache/ms-playwright` `actions/cache` entry `build` populated while compiling `VisualTests.csproj` (the *browser* binaries - Chrome for Testing + Chrome Headless Shell, ~290 MiB combined, via `InstallPlaywrightBrowsers`'s `AfterTargets="Build"` MSBuild target - live outside the repo tree and don't travel in the artifact; `needs: build` guarantees this restore is a hit). The Playwright *driver* (a bundled Node runtime under `bin/.../.playwright/`, distinct from the browser) does travel in the artifact, but only because `include-hidden-files: true` is set - `upload-artifact` excludes dot-prefixed paths by default, which silently dropped that whole folder on the first real run and failed `visual-tests` with "Driver not found" (confirmed by downloading that run's actual artifact and listing its contents, not guessing). `publish.yml` mirrors this exact shape (`backend-build` -> `backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, no `backend-format-check` equivalent since `publish.yml` has no formatting gate). The consolidation is deliberate at the cost of a ~150-200 MiB compressed artifact transferred to every downstream job (measured against a real run) in exchange for one restore+build instead of four - see `docs/TDRs/2_slow_ci_pipeline.adoc` for the wall-clock trade-off this accepts and the full history of what broke getting here.
- **Test projects:** Application.UnitTests, ArchitectureTests, IntegrationTests, VisualTests
- **Why `dotnet run` not `dotnet test`:** TUnit uses Microsoft.Testing.Platform; `dotnet test` on .NET 10 requires opt-in to new experience. `dotnet run` invokes the test runner directly.
- **Typical duration:** previously ~10 minutes end to end (median ~9.6 min; grew from ~4.5 min in May 2026) with all four test projects running sequentially in one job, dominated by `VisualTests` (~57% of the run: boots the full Aspire stack, then drives it with Playwright). As of issue #773, that single job was split into three parallel jobs, and `VisualTests` itself no longer drives a real interactive Keycloak login for most of its 117 tests (`AuthHelper.FastSignInAsync` mints a token directly instead - see `docs/TDRs/2_slow_ci_pipeline.adoc`). First real run of that split (2026-07-19, n=1): `format-check` ~1 min, `fast-tests` ~1 min, `integration-tests` ~4 min, `visual-tests` ~6 min, all parallel - critical path ~6 min. The `build`-then-fan-out shape (2026-07-28) changes that math again: critical path is now `build` (~1-2 min) plus whichever of `fast-tests`/`integration-tests`/`visual-tests` takes longest, instead of each of those paying its own restore+build inside its own parallel timeline - a deliberate trade of a bit of wall-clock time for one restore+build instead of three (see the TDR for the reasoning and the ~200 MiB compressed artifact this costs per run). Not yet measured over a real run at the time of writing - treat both sets of numbers above as preliminary until re-measured (n=20+), same methodology as the TDR's original figure. When polling for this workflow's checks, don't re-poll more often than every ~2-3 minutes while it's in progress, and poll `build` plus all four downstream checks (`format-check`/`fast-tests`/`integration-tests`/`visual-tests`), since all four now gate on `build` finishing first.

### `frontend.yml` / `frontend-checks.yml`
- **Trigger:** `frontend/**`, `.github/workflows/frontend.yml`, or `.github/workflows/frontend-checks.yml` path filter, or manual
- `frontend.yml` itself is a thin caller (`jobs.checks: uses: ./.github/workflows/frontend-checks.yml`) - all the actual work lives in the reusable `frontend-checks.yml` workflow, which `publish.yml`'s `frontend-checks` job also calls (see below) so the PR/push gate and the release gate run identical steps and cannot drift apart (#1733). The two callers pass different VITE_*/STORAGE_PUBLIC_URL origins to the smoke test below - `frontend.yml` passes fake `*.example.test` values (no access to real infra), `publish.yml` passes the real `maik-hasler.de` ones the release will actually be deployed with - and `publish.yml`'s caller additionally has `needs: [backend-fast-tests, backend-integration-tests, backend-visual-tests]`, so it cannot start until those three backend test jobs finish; `frontend.yml`'s caller has no such gate and starts as soon as its own trigger fires.
- **Jobs:**
  - `lint` (`pnpm lint` + `pnpm format:check` + `pnpm i18n:check` + the `check:nginx-*`/`check:config-defer`/`check:pwa-precache`/`check:pwa-manifest` scripts + `pnpm check` type check)
  - `test` (`pnpm test`, Vitest, `src/**/*.test.ts` - currently the `lib/` pure-function suite), `needs: lint`
  - `build` (`pnpm build`), `needs: [lint, test]`
  - `docker-image` ("Production container smoke test" - builds `frontend/Dockerfile` for real, boots it, and asserts `config.js` substitution plus the CSP/gzip_static/web-app-manifest behavior asserted in its own "Content-Security-Policy header reflects runtime origins"/"gzip_static serves precompressed assets"/"Web app manifest is served with a complete install listing" steps; the manifest one is the served-artifact half of `check:pwa-manifest` above, which only sees what `vite.config.ts` declares), `needs: [lint, test]` - this is the only place `frontend/Dockerfile` is ever actually built and run in CI; parameterized via `workflow_call` inputs (`vite_keycloak_authority_url`, `vite_keycloak_client_id`, `keycloak_origin`, `vite_api_url`, `storage_public_url`) so both callers can point it at their own origins
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

**Full release:** Tag without `-rc` suffix → image published + `latest` tag updated, and `deploy-production` runs.

### Publish flow (backend/frontend/keycloak)
1. Run full test suite - three parallel jobs (`backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, same split as `dotnet.yml`) that `publish-backend`, `publish-frontend`, and `publish-keycloak` all wait on via `needs:` before building anything, so a test failure blocks every image, not just the backend's. `publish-frontend` additionally `needs:` the `frontend-checks` job, which calls the reusable `frontend-checks.yml` workflow (lint/format/i18n/nginx-header checks + type-check + unit tests + a production-container smoke test against the real `maik-hasler.de` origins) - before #1733 `publish-frontend` ran only `pnpm lint`/`check`/`test`/`build` inline and never built or smoke-tested the production nginx image at all, so the release path skipped exactly the checks guarding the artifact it was about to push; keycloak has no additional test gate beyond the shared backend suite
2. Login to GHCR
3. Extract version from tag (strips leading `v`)
4. Build and push Docker image
5. Tag with version + `latest` (if not RC) or `staging` (if RC) - `publish-backend` also exposes this version string as a job output (`outputs.version`), which `deploy-staging`/`deploy-production` below consume to deploy an immutable tag instead of a floating one
6. `github-release` job (`needs: [publish-backend, publish-frontend, publish-keycloak]`, so it runs for both stable and RC tags - unlike `deploy-staging`/`deploy-production`, which are gated on `prerelease`) creates a GitHub Release for the tag via `gh release create`, using the default `GITHUB_TOKEN` (job-scoped `permissions: contents: write`, no `RELEASE_TOKEN` needed - creating a release doesn't push a ref, so it doesn't hit the "tags pushed with `GITHUB_TOKEN` don't trigger workflows" restriction that `release-rc.yml` works around). Notes are generated from commit subjects since the previous tag (found by semver sort across both stable and RC tags, via `git tag --sort=-v:refname`), grouped by Conventional Commit type - a `!` after the type/scope (e.g. `feat!:`) surfaces under a "Breaking Changes" section. See [VERSIONING.md](../VERSIONING.md)'s "Release Notes" section - GitHub Releases is the canonical record, there is no `CHANGELOG.md` file.
7. `deploy-staging` (RC tags) or `deploy-production` (stable tags) deploys - see below

### `deploy-staging` vs `deploy-production` (#1344)

Before #1344, only `deploy-staging` existed, gated on `prerelease == 'true'` - a stable tag published images and then did nothing, silently leaving whatever RC was last deployed running. `deploy-production` closes that gap, gated on `prerelease == 'false'`.

**They are two separate jobs/GitHub Environments, but currently deploy to the exact same host and `/opt/einsatzbereit` directory** - see AGENTS.md/README.md's Test Users note: there is only one live host today (`einsatzbereit.maik-hasler.de`), and it is deliberately staging/demo infrastructure, not a hardened production box. The split exists so that whenever a genuinely separate production host is provisioned, pointing at it is only a matter of changing the `production` GitHub Environment's secrets - no workflow restructuring. Until then, a stable release simply replaces whatever RC was running on that one host with the same images tagged under a new version (`deploy-staging` and `deploy-production` share a concurrency group precisely because they mutate the same host).

`deploy-production` differs from `deploy-staging` in two deliberate ways beyond which images get deployed: it does not set `DATABASE_SEED_ON_STARTUP` (a stable release should not reseed demo data over whatever real data exists), and it sets `ASPNETCORE_ENVIRONMENT=Production` instead of `Staging`.

**Rollback and `IMAGE_TAG` (#1733).** Both jobs render `docker-compose.yml`'s `IMAGE_TAG` to the exact version this tag push just built (`needs.publish-backend.outputs.version`), not a floating `staging`/`latest` tag - a floating tag is overwritten by every deploy including a bad one, so there would be no immutable artifact left to roll back to. Before rendering the new `.env` (and before "Copy compose file and .env" overwrites the host's old one), a "Capture previously deployed image tag" step SSHes in and reads the *current* `IMAGE_TAG` out of the host's existing `.env` into a step output - this is the only point at which the previous version is still readable, since GHCR always keeps the specific-version tag (as opposed to `staging`/`latest`) immutable and pull-able later. "Rollback on unhealthy deploy" uses that captured value: if the post-deploy health gate fails, it `sed`s the host's `.env` back to `IMAGE_TAG="<previous version>"`, re-pulls, and redeploys - a specific prior release, not whatever a floating tag happens to resolve to. The captured tag is passed through the step's `env:` block and then to the remote shell as an `ssh`-command-line environment variable (`${PREV_TAG@Q}`-quoted, same pattern as `GHCR_USERNAME` in "Pull and restart" above) rather than spliced directly into the heredoc script text - it's read live off a file the workflow doesn't fully control (an operator's out-of-band incident edit), so it's treated as untrusted input, not embedded as literal script syntax. If no previous tag was recorded (the very first deploy to a fresh host), rollback logs a warning and exits cleanly instead of failing - there is genuinely nothing to revert to yet.

The rollback step's `if:` condition also changed from `steps.pull_and_restart.outcome == 'success'` to `steps.pull_and_restart.conclusion != 'skipped'`. The failure mode the "Rollback on unhealthy deploy" step's own comment describes (the "Capture container logs on deploy failure" step's comment only says the deploy fails inside `docker compose up` when the backend never becomes healthy, without the `depends_on` detail below) - `docker compose up` failing because the backend never reports healthy (frontend/keycloak's `depends_on: condition: service_healthy` on it never resolves) - is `pull_and_restart` *failing*, not succeeding. Gating on `outcome == 'success'` meant rollback never fired in exactly that scenario; `conclusion != 'skipped'` fires whenever the deploy step actually ran, whether it succeeded or failed, while still skipping rollback when an earlier step (env render, file copy, Keycloak mapper setup) failed first and no new images were ever deployed.

**Required setup before the first stable release:** create a `production` GitHub Environment and add `PRODUCTION_SSH_KEY`, `PRODUCTION_SSH_HOST_KEY`, `PRODUCTION_SSH_USER`, and `PRODUCTION_SSH_HOST` secrets - for now, the same values as the `staging` Environment's `STAGING_SSH_*` equivalents, since it is the same host (see the `staging` Environment secrets below for how `STAGING_SSH_HOST_KEY` was captured; do the same capture once and reuse the result for both). All other secrets `deploy-production` uses (`POSTGRES_*`, `KEYCLOAK_*`, `MINIO_*`, `SMTP_*`, `GRAFANA_*`, `ALERT_NOTIFICATION_EMAIL`, `GHCR_USERNAME`, `GHCR_TOKEN`) are unprefixed and shared with `deploy-staging` - if any of those are currently scoped only to the `staging` Environment rather than the repository, they need to be added to `production` too before the first stable tag, or that job will fail rendering `.env`.

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
2. Once `deploy-staging` (RC tag) or `deploy-production` (stable tag) reports success, smoke-test live: `curl https://api.maik-hasler.de/health`, then HEAD-check `https://einsatzbereit.maik-hasler.de`.
3. If any publish job fails, diagnose from logs; if the deploy step itself fails, the SSH/GHCR secrets in the relevant GitHub Environment (`staging` or `production` - see "`deploy-staging` vs `deploy-production`" above) are the most likely cause.

**Required `staging` Environment secret:** `KEYCLOAK_BACKEND_SECRET` - a randomly generated value (not a committed literal), used both as the `backend` Keycloak client's real secret (resolved into the `${KEYCLOAK_BACKEND_SECRET}` placeholder in `keycloak/realms/einsatzbereit-realm.json` at realm-import time) and as the backend app's `Keycloak__ClientSecret`. See `keycloak/AGENTS.md`. If unset, `deploy-staging` fails loudly (docker compose's `:?` guard in `docker-compose.yml`) rather than deploying with a weak default.

**Required `staging` Environment secret:** `STAGING_SSH_HOST_KEY` - the pinned SSH host key `deploy-staging` trusts, instead of accepting whatever `ssh-keyscan` returns live on every run (a DNS/BGP hijack of `STAGING_SSH_HOST` would otherwise have its host key trusted automatically). Capture it once, out-of-band, from a connection you've already verified is the real host:
```bash
ssh-keyscan -H <staging-host> > host_key.txt
# Verify the key fingerprint against the hosting provider's console/docs
# before trusting it, then store the file's contents as the secret.
```
Rotating the staging host's SSH host key (a fresh box, a reinstall) requires updating this secret to match.

**Required `staging` Environment secrets:** `MINIO_APP_ACCESS_KEY` / `MINIO_APP_SECRET_KEY` - a generated (not MinIO root) credential pair the `minio-init` compose service provisions on first deploy, scoped to only the `einsatzbereit` bucket. The backend authenticates with these instead of `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` (#1353) - a leaked value can then only read/write that one bucket, not administer the whole MinIO instance.

**Required `staging`/`production` Environment secrets:** `OFFSITE_S3_ENDPOINT` / `OFFSITE_S3_ACCESS_KEY` / `OFFSITE_S3_SECRET_KEY` - point `minio-backup`'s offsite leg (#1087) at the `einsatzbereit-backups` S3-compatible Object Storage bucket, mirroring `postgres_backups`/`minio_backups` off the host daily. A distinct bucket/credential pair from anything MinIO-related above - this is a backup destination, not application storage. Shared across both Environments since staging and production are the same host today (see "`deploy-staging` vs `deploy-production`" above).

## Reset Workflow (manual)

`reset-staging.yml` wipes all staging test data - Postgres (`einsatzbereit` + `keycloak` databases, one instance) and MinIO uploads - then restarts the stack. It does **not** run `docker compose pull`, so the exact image tags/versions already running come back up unchanged; only data is reset.

- Trigger: `workflow_dispatch` only, with a required `confirm` input that must equal exactly `RESET`
- Runs in the `staging` GitHub Environment, reusing the same SSH secrets as `deploy-staging` - including `STAGING_SSH_HOST_KEY`: like `deploy-staging`, it trusts a host key pinned from a secret rather than whatever `ssh-keyscan` returns live (#1733 - this is a destructive workflow, so it should be at least as strict about a DNS/BGP hijack of `STAGING_SSH_HOST` as the deploy jobs are, not less)
- Removes the `postgres_data`, `minio_data`, and `grafana_data` volumes - `postgres_backups` and `minio_backups` are left alone on purpose, as the recovery path if a reset needs to be walked back
- Before rendering the new `.env`, a "Capture currently deployed image tag" step reads the host's *current* `IMAGE_TAG` and carries it into the re-rendered `.env` (#1733) - without this, the render had no `IMAGE_TAG` line at all, so `docker-compose.yml`'s `${IMAGE_TAG:-staging}` default silently took over on restart, which could resurrect a stale cached `staging`-tagged image instead of whatever version (RC or stable) was actually running before the reset
- Because Keycloak's realm import runs with `OVERWRITE_EXISTING` (see `deploy-staging` in `publish.yml`) and the backend has `Database__MigrateOnStartup: true`, the stack re-migrates and re-imports `keycloak/realms/einsatzbereit-realm.json` on restart - the standard `vera`/`olaf`/`admin` test accounts come back automatically since they are defined in that checked-in realm config, not created ad hoc. This is by design: staging's demo credentials (including full-admin `admin`) are meant to be publicly known and instantly restorable, not rotated or hardened - see `keycloak/AGENTS.md` and #1166
- The backend also has `Database__SeedOnStartup` resolved from `DATABASE_SEED_ON_STARTUP` (see `docker-compose.yml`), which `deploy-staging` hardcodes to `"true"` directly in the rendered `.env` (not a secret - see `publish.yml`'s "Render .env" step) - defaults to `false` for any other deployment of the same compose file (#1375). On staging this means `ApplicationDbContextInitializer.SeedAsync()` runs on restart alongside the migrate/backfill calls, so staging comes back with the standard seeded organizations and published opportunities instead of an empty database. `SeedAsync()` is idempotent (no-ops if any `Organization` row already exists), so this is safe on every reset and on ordinary redeploys
- Ends with the same post-deploy health gate (`/health` polling) used by `deploy-staging`

## Issue Templates

```
.github/ISSUE_TEMPLATE/
├── bug_report.yml   [Bug]: prefix, label: bug
├── chore.yml        [Chore]: prefix, label: chore
├── user_story.yml   [Story]: prefix, label: user-story
└── config.yml       blank_issues_enabled: false (no config for the templates above)
```

All templates are in **English** (see `CONTRIBUTING.md`'s Language Convention). Fields:

**Bug report:** Affected Persona (dropdown: Volunteer Vera/Organizer Olaf/Platform Admin/Contributor Caro/Maintainer Milo/All - same order as the stakeholder table in arc42 chapter 1, end users before project-side roles), Priority (Low/Medium/High), Description, Steps to Reproduce, Environment, Additional Information

**Chore:** Priority, Description, Acceptance Criteria (checkboxes)

**User story:** Persona (dropdown, same options as Bug report minus "All"), Priority, User Story (As/I want/so that), Description, Acceptance Criteria (Given/When/Then), Implementation Proposal, Additional Information

## Review & PR Template

- `.github/CODEOWNERS` - `* @maik-hasler`, so review is auto-requested on every PR instead of relying on contributors to remember (see `CONTRIBUTING.md` "Pull Request Process")
- `.github/PULL_REQUEST_TEMPLATE.md` - prompts for What/Why, the issue link, a Testing section, and the "Live verification" section required by root `AGENTS.md`'s deploy-and-verify flow

## Notes

- Path filters prevent unnecessary builds (backend change → only `dotnet.yml` runs)
- Keycloak shares the same unified tag as backend/frontend; the upstream Keycloak version is tracked separately as an OCI label (`org.opencontainers.image.base.version`), extracted from `keycloak/Dockerfile`'s `FROM` line
- GitHub Pages deployment uses `permissions: pages: write, id-token: write` with concurrency group to cancel stale deployments
