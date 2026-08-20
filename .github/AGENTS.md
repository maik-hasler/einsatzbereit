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
- **Jobs:** `build` (single `dotnet restore` + `dotnet build` for the whole `Einsatzbereit.slnx`, uploaded as the `backend-build` artifact via `actions/upload-artifact` with `include-hidden-files: true`), then `format-check`, `fast-tests` (Application.UnitTests + ArchitectureTests), `integration-tests` (IntegrationTests, needs pnpm + Docker pre-pull for the Aspire stack), and `visual-tests` (VisualTests, same setup as `integration-tests`, plus a 4-way `shard` matrix - see below) all run in parallel with `needs: build`, each downloading that one artifact instead of restoring/building its own copy, then running its test project(s) via `dotnet run --project ... --no-restore --no-build`. `visual-tests` additionally restores its own Playwright browser cache. `publish.yml` mirrors this exact shape (`backend-build` -> `backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, no `backend-format-check` equivalent since `publish.yml` has no formatting gate). See `docs/TDRs/2_slow_ci_pipeline.adoc` for the wall-clock trade-off this consolidation accepts and the history of what broke getting here (NuGet cache still needed alongside the prebuilt artifact, the executable-bit and hidden-file gotchas in `actions/upload-artifact`, the Playwright download size).
- **Test projects:** Application.UnitTests, ArchitectureTests, IntegrationTests, VisualTests
- **`visual-tests` is sharded (#2145):** the job is a `fail-fast: false` matrix over `shard: [1, 2, 3, 4]`, so it reports as four checks (`visual-tests (1)` ... `visual-tests (4)`) rather than one - **if branch protection lists `visual-tests` as a required check, that name no longer exists and has to be updated to the four matrix check names.** Each shard boots its own Aspire stack and runs roughly a quarter of the suite, selected by a Microsoft.Testing.Platform `--treenode-filter` over class names. TUnit has no native sharding flag, so the filter is computed per run by `.github/scripts/visual-test-shard.sh`, which packs test classes longest-first into the lightest shard - never hand-write a shard list, or a newly added test class silently stops running everywhere. The shard count lives only in the `shard:` list; the script reads it from `strategy.job-total`. `publish.yml`'s `backend-visual-tests` is sharded identically and the two must stay in step.
- **Why `dotnet run` not `dotnet test`:** TUnit uses Microsoft.Testing.Platform; `dotnet test` on .NET 10 requires opt-in to new experience. `dotnet run` invokes the test runner directly.
- **Typical duration:** critical path is `build` plus whichever of `fast-tests`/`integration-tests`/`visual-tests` takes longest, dominated by `VisualTests` (Aspire stack + Playwright) - see `docs/TDRs/2_slow_ci_pipeline.adoc` for the measured numbers and their trend. When polling for this workflow's checks, don't re-poll more often than every ~2-3 minutes while it's in progress, and poll `build` plus all downstream checks (`format-check`/`fast-tests`/`integration-tests`, and all four `visual-tests` shards), since they all gate on `build` finishing first.

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

All components share a single unified repo-level tag, published to GHCR - see [VERSIONING.md](../VERSIONING.md) for the tag format and image names.

**Release candidates:** `-rc.N` suffix (e.g., `v1.0.0-rc.1`). Image is published, tagged `staging` instead of `latest`, and `deploy-staging` runs.

**Full release:** Tag without `-rc` suffix → image published + `latest` tag updated, and `deploy-production` runs.

### Publish flow (backend/frontend/keycloak)
1. Run full test suite - three parallel jobs (`backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, same split as `dotnet.yml`) that `publish-backend`, `publish-frontend`, and `publish-keycloak` all wait on via `needs:` before building anything, so a test failure blocks every image, not just the backend's. `publish-frontend` additionally `needs:` the `frontend-checks` job, which calls the reusable `frontend-checks.yml` workflow (lint/format/i18n/nginx-header checks + type-check + unit tests + a production-container smoke test against the real `maik-hasler.de` origins) - see that workflow's own header comment for what `publish-frontend` skipped before #1733; keycloak has no additional test gate beyond the shared backend suite
2. Login to GHCR
3. Extract version from tag (strips leading `v`)
4. Build and push Docker image
5. Tag with version + `latest` (if not RC) or `staging` (if RC) - `publish-backend` also exposes this version string as a job output (`outputs.version`), which `deploy-staging`/`deploy-production` below consume to deploy an immutable tag instead of a floating one
6. `github-release` job (`needs: [publish-backend, publish-frontend, publish-keycloak]`, so it runs for both stable and RC tags - unlike `deploy-staging`/`deploy-production`, which are gated on `prerelease`) creates a GitHub Release for the tag via `gh release create`, using the default `GITHUB_TOKEN` (job-scoped `permissions: contents: write`, no `RELEASE_TOKEN` needed - creating a release doesn't push a ref, so it doesn't hit the "tags pushed with `GITHUB_TOKEN` don't trigger workflows" restriction that `release-rc.yml` works around). Notes are generated from commit subjects since the previous tag (found by semver sort across both stable and RC tags, via `git tag --sort=-v:refname`), grouped by Conventional Commit type - a `!` after the type/scope (e.g. `feat!:`) surfaces under a "Breaking Changes" section. See [VERSIONING.md](../VERSIONING.md)'s "Release Notes" section - GitHub Releases is the canonical record, there is no `CHANGELOG.md` file.
7. `deploy-staging` (RC tags) or `deploy-production` (stable tags) deploys - see below

### `deploy-staging` vs `deploy-production` (#1344)

`deploy-production` (gated on `prerelease == 'false'`) closes the gap where a stable tag previously published images and then deployed nothing - see `publish.yml`'s comment above the `deploy-production` job for the #1344 history.

**They are two separate jobs/GitHub Environments, but currently deploy to the exact same host and `/opt/einsatzbereit` directory** - there is only one live host today (`einsatzbereit.maik-hasler.de`; see root `AGENTS.md`'s Test Users note for why). The split exists so that whenever a genuinely separate production host is provisioned, pointing at it is only a matter of changing the `production` GitHub Environment's secrets - no workflow restructuring. Until then, a stable release simply replaces whatever RC was running on that one host with the same images tagged under a new version (`deploy-staging` and `deploy-production` share a concurrency group precisely because they mutate the same host).

`deploy-production` differs from `deploy-staging` in two deliberate ways beyond which images get deployed: it does not set `DATABASE_SEED_ON_STARTUP` (a stable release should not reseed demo data over whatever real data exists), and it sets `ASPNETCORE_ENVIRONMENT=Production` instead of `Staging`.

**Rollback and `IMAGE_TAG` (#1733).** Both jobs render `docker-compose.yml`'s `IMAGE_TAG` to the exact version this tag push just built (`needs.publish-backend.outputs.version`), not a floating `staging`/`latest` tag, so a failed deploy can roll back to a specific prior release instead of whatever a floating tag happens to resolve to - see `publish.yml`'s "Capture previously deployed image tag" and "Rollback on unhealthy deploy" step comments for the mechanics.

The rollback step's `if:` condition is `steps.pull_and_restart.conclusion != 'skipped'`, not `outcome == 'success'` - see the "Rollback on unhealthy deploy" step's own comment in `publish.yml` for why.

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

`release-rc.yml` validates the branch suffix, promotes it to an annotated tag pushed with `RELEASE_TOKEN`, and deletes the branch - see the workflow's own top-of-file comment for the full mechanics. After the tag exists, `publish.yml` runs end-to-end (build -> GHCR -> `deploy-staging` for RC tags).

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
- Runs in the `staging` GitHub Environment, reusing the same SSH secrets as `deploy-staging` - including the pinned `STAGING_SSH_HOST_KEY` described above (#1733 - this is a destructive workflow, so it should be at least as strict about a DNS/BGP hijack of `STAGING_SSH_HOST` as the deploy jobs are, not less)
- Removes the `postgres_data`, `minio_data`, and `grafana_data` volumes - `postgres_backups` and `minio_backups` are left alone on purpose, as the recovery path if a reset needs to be walked back
- Before rendering the new `.env`, a "Capture currently deployed image tag" step reads the host's *current* `IMAGE_TAG` and carries it into the re-rendered `.env` (#1733) - without this, the render had no `IMAGE_TAG` line at all, so `docker-compose.yml`'s `${IMAGE_TAG:-staging}` default silently took over on restart, which could resurrect a stale cached `staging`-tagged image instead of whatever version (RC or stable) was actually running before the reset
- Because Keycloak's realm import runs with `OVERWRITE_EXISTING` (see `deploy-staging` in `publish.yml`) and the backend has `Database__MigrateOnStartup: true`, the stack re-migrates and re-imports `keycloak/realms/einsatzbereit-realm.json` on restart - the standard `vera`/`olaf`/`admin` test accounts come back automatically since they are defined in that checked-in realm config, not created ad hoc. This is by design: staging's demo credentials (including full-admin `admin`) are meant to be publicly known and instantly restorable, not rotated or hardened - see `keycloak/AGENTS.md` and #1166
- The backend also has `Database__SeedOnStartup` resolved from `DATABASE_SEED_ON_STARTUP` (see `docker-compose.yml`), which `deploy-staging` hardcodes to `"true"` directly in the rendered `.env` (not a secret - see `publish.yml`'s "Render .env" step) - defaults to `false` for any other deployment of the same compose file (#1375). On staging this means `ApplicationDbContextInitializer.SeedAsync()` runs on restart alongside the migrate/backfill calls, so staging comes back with the standard seeded organizations and published opportunities instead of an empty database. `SeedAsync()` is idempotent (no-ops if any `Organization` row already exists), so this is safe on every reset and on ordinary redeploys - but read that no-op as more than a safety property: **a changed seed set never reaches a database that already has data.** An edit to the seed set applies to a fresh database and to nothing else, so a long-lived environment keeps serving whatever it was first seeded with no matter how many times it redeploys, and this reset workflow is the only thing that applies an updated seed set to staging (#1776 - see `ApplicationDbContextInitializer.SeedAsync()`'s own comment for the incident this guards against). `SeedAsync()` logs a warning naming the organization count it found and this workflow whenever seeding is skipped, so that is visible in the backend's startup logs instead of looking identical to a successful one
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
