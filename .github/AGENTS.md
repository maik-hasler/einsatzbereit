# .github - CI & Issue Templates

## Workflows

```
.github/workflows/
├── dotnet.yml                  Backend: build + test
├── frontend.yml                Frontend: calls frontend-checks.yml (test-domain values)
├── frontend-checks.yml         Reusable (workflow_call): lint → test/build/container-smoke-test - shared by frontend.yml and publish.yml so the release path can't skip checks the PR gate runs (#1733)
├── docs.yml                    Docs: AsciiDoc build (push + PR) → GitHub Pages deploy (push only)
├── keycloak-realm-import.yml   Verifies the committed realm still imports on the Keycloak version the released image is built from, and that its ${VAR} placeholders actually resolve
├── publish.yml                 Tag-triggered: build + push backend/frontend/keycloak to GHCR, create a GitHub Release - see "Publish Workflows" below
├── release-rc.yml              Promotes a release/v* branch into a real tag (used by Claude Code on the web)
├── mutation-tests.yml          Manual (workflow_dispatch): Stryker.NET over Domain + Application and StrykerJS over frontend src/lib, report-only
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
- `frontend.yml` itself is a thin caller (`jobs.checks: uses: ./.github/workflows/frontend-checks.yml`) - all the actual work lives in the reusable `frontend-checks.yml` workflow, which `publish.yml`'s `frontend-checks` job also calls (see below) so the PR/push gate and the release gate run identical steps and cannot drift apart (#1733). Both callers pass the same fake `*.example.test` VITE_*/STORAGE_PUBLIC_URL origins to the smoke test below - the released image resolves its real origins from environment variables at container start, so no real origin is ever baked into a build. `publish.yml`'s caller additionally has `needs: [backend-fast-tests, backend-integration-tests, backend-visual-tests]`, so it cannot start until those three backend test jobs finish; `frontend.yml`'s caller has no such gate and starts as soon as its own trigger fires.
- **Jobs:**
  - `lint` (`pnpm lint` + `pnpm format:check` + `pnpm i18n:check` + the `check:nginx-*`/`check:config-defer`/`check:pwa-precache`/`check:pwa-manifest` scripts + `pnpm check` type check)
  - `test` (`pnpm test`, Vitest, `src/**/*.test.ts` - currently the `lib/` pure-function suite), `needs: lint`
  - `build` (`pnpm build`), `needs: [lint, test]`
  - `docker-image` ("Container smoke test" - builds `frontend/Dockerfile` for real, boots it, and asserts `config.js` substitution plus the CSP/gzip_static/web-app-manifest behavior asserted in its own "Content-Security-Policy header reflects runtime origins"/"gzip_static serves precompressed assets"/"Web app manifest is served with a complete install listing" steps; the manifest one is the served-artifact half of `check:pwa-manifest` above, which only sees what `vite.config.ts` declares), `needs: [lint, test]` - this is the only place `frontend/Dockerfile` is ever actually built and run in CI; parameterized via `workflow_call` inputs (`vite_keycloak_authority_url`, `vite_keycloak_client_id`, `keycloak_origin`, `vite_api_url`, `storage_public_url`) so a caller can vary the origins it asserts against
- **No E2E job** - E2E lives in backend `tests/VisualTests/` (run by `dotnet.yml`)

### `docs.yml`
- **Trigger:** `docs/**` path filter, push/PR to main, or manual
- **Jobs:** build AsciiDoc (`build`) -> deploy to GitHub Pages (`deploy`, `needs: build`)
- `deploy` is gated with `if: github.event_name == 'push'` so PRs only build and never deploy
- Uses `tonynv/asciidoctor-action` with `asciidoctor-diagram` for PlantUML
- `build` uses a per-ref concurrency group (`docs-build-${{ github.ref }}`) separate from `deploy`'s `pages` group, so a PR build can't cancel an in-progress Pages publish

## Publish Workflows (tag-triggered)

All components share a single unified repo-level tag, published to GHCR - see [VERSIONING.md](../VERSIONING.md) for the tag format and image names.

**Release candidates:** `-rc.N` suffix (e.g., `v1.0.0-rc.1`). Images are published under the version tag only; `latest` is not moved.

**Full release:** Tag without `-rc` suffix → images published + `latest` tag updated.

Publishing is where this repository's involvement ends. Nothing here runs, hosts, or reaches into a running environment, and nothing here holds a credential for one.

### Publish flow (backend/frontend/keycloak)
1. Run full test suite - three parallel jobs (`backend-fast-tests`/`backend-integration-tests`/`backend-visual-tests`, same split as `dotnet.yml`) that `publish-backend`, `publish-frontend`, and `publish-keycloak` all wait on via `needs:` before building anything, so a test failure blocks every image, not just the backend's. `publish-frontend` additionally `needs:` the `frontend-checks` job, which calls the reusable `frontend-checks.yml` workflow (lint/format/i18n/nginx-header checks + type-check + unit tests + a container smoke test) - see that workflow's own header comment for what `publish-frontend` skipped before #1733; keycloak has no additional test gate beyond the shared backend suite
2. Login to GHCR
3. Extract version from tag (strips leading `v`)
4. Build and push Docker image
5. Tag with version, plus `latest` if the tag is not an RC - `publish-backend` also exposes this version string as a job output (`outputs.version`), consumed by `github-release` below. Nothing downstream of this consumes it further
6. `github-release` job (`needs: [publish-backend, publish-frontend, publish-keycloak]`, so it runs for both stable and RC tags, and is the last job in this workflow) creates a GitHub Release for the tag via `gh release create`, using the default `GITHUB_TOKEN` (job-scoped `permissions: contents: write`, no `RELEASE_TOKEN` needed - creating a release doesn't push a ref, so it doesn't hit the "tags pushed with `GITHUB_TOKEN` don't trigger workflows" restriction that `release-rc.yml` works around). Notes are generated from commit subjects since the previous tag (found by semver sort across both stable and RC tags, via `git tag --sort=-v:refname`), grouped by Conventional Commit type - a `!` after the type/scope (e.g. `feat!:`) surfaces under a "Breaking Changes" section. See [VERSIONING.md](../VERSIONING.md)'s "Release Notes" section - GitHub Releases is the canonical record, there is no `CHANGELOG.md` file.

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

`release-rc.yml` validates the branch suffix, promotes it to an annotated tag pushed with `RELEASE_TOKEN`, and deletes the branch - see the workflow's own top-of-file comment for the full mechanics. After the tag exists, `publish.yml` runs end-to-end (test -> build -> GHCR -> GitHub Release) and stops there.

**One-time setup the user must do:**

- Create a fine-grained Personal Access Token scoped to this repo with `contents: write`.
- Add it as a **repository secret** named `RELEASE_TOKEN`.

A PAT (not the default `GITHUB_TOKEN`) is mandatory because tags pushed with `GITHUB_TOKEN` do not trigger downstream workflows - GitHub explicitly prevents that to avoid workflow loops. Without `RELEASE_TOKEN`, `release-rc.yml` will fail at checkout.

**After pushing the branch:**

1. Poll the publish workflow's checks for the new tag (via `mcp__github__get_commit` → check_runs, or fetch `https://api.github.com/repos/{owner}/{repo}/commits/{sha}/check-runs`), until `publish-backend`/`publish-frontend`/`publish-keycloak` all report success.
2. If any publish job fails, diagnose from that job's logs here. Once all three are green the tag's images exist on GHCR and the release is done as far as this repository is concerned.

`RELEASE_TOKEN` is the only repository secret this repo needs. There are no environment secrets and no GitHub Environments - nothing here connects to a running instance of the app.

## Mutation Testing Workflow (manual)

`mutation-tests.yml` runs Stryker.NET over the two layers that `Application.UnitTests` can drive without Docker, plus a separate StrykerJS job over the frontend.

- **Trigger:** `workflow_dispatch` only - never on push or pull request, so it can never gate a merge
- **Jobs:** one `mutation-tests` job, a `fail-fast: false` matrix over `project: [Domain, Application]`, so it reports as two checks. Each leg installs `dotnet-stryker` (pinned to 4.16.0) and runs it from `backend/tests/Application.UnitTests/`, mutating one source project while `Application.UnitTests` (1017 tests, no Docker) does the killing. Measured ~4 min (`Domain`) and ~8 min (`Application`)
- **Report-only (#2147), by two independent mechanisms:** the workflow is manual so it is never a required check, and `stryker-config.json` pins `thresholds.break` to `0` so a low score exits 0 anyway. Same posture as the coverage summary in `fast-tests` (#1327). **Do not add a break threshold** - the whole point of the number is that it is allowed to move
- **Why manual and not on the PR path:** it is roughly two orders of magnitude more expensive than the `fast-tests` job whose test suite it reuses. It exists as the guardrail for the E2E rebalance (#2148) - record the score, migrate a wave of end-to-end tests down the pyramid, re-run, and if the score held, the removed coverage was redundant. See `docs/TDRs/2_slow_ci_pipeline.adoc` for the recorded baseline, the wall-clock, and how to read the number
- **Run it from `backend/tests/Application.UnitTests/`, never from `backend/`** - from `backend/` Stryker finds `Einsatzbereit.slnx`, switches to solution mode, and builds the whole solution including `VisualTests` and its ~290 MiB Playwright download, for a run that never opens a browser
- **`concurrency: 1` in `stryker-config.json` is load-bearing, not a performance knob (#2147).** Stryker's Microsoft.Testing.Platform runner is preview, and above concurrency 1 it reports kills that never happened: 30-73 extra kills per run, never a lost one, and a different subset every time (across four `Domain` runs the spurious sets intersected in zero mutants). That inflates the score by ~5 points and jitters it by ~1. At concurrency 1 two runs at the same commit are bit-for-bit identical. Raising it to use the runner's spare cores destroys the only property that makes the number worth recording
- **`Application.UnitTests.csproj` references `Domain.csproj` directly** even though Application already pulls it in transitively. That reference exists only so Stryker can resolve `--project Domain.csproj`; see the comment in the csproj before removing it as redundant

### The frontend leg (`mutation-tests-frontend`, #2148)

A separate job rather than a matrix leg on the one above - the two toolchains share nothing but the trigger and the report-only posture. StrykerJS (`@stryker-mutator/core` + `@stryker-mutator/vitest-runner`, both pinned in `frontend/package.json`) mutating `frontend/src/lib/**` while the Vitest suite does the killing. Config: `frontend/stryker.config.json`; run locally with `pnpm mutation`.

- **Scoped to `src/lib/**` on purpose, and that scope is the whole design decision.** Mutating all of `src/**` instruments 182 files into **14 851 mutants** - 5.5x the backend's 2 690, against a suite that renders components in jsdom rather than calling pure functions. A full pass is hours. `src/lib/**` is 37 files / 897 mutants of pure logic, which is the tier that is both cheap to mutate and directly comparable to the backend's `Domain`/`Application`
- **The component tier is on-demand, not scheduled:** `pnpm mutation --mutate "src/components/Foo.tsx"` scores one component in minutes. That is how the per-component figures in `docs/TDRs/2_slow_ci_pipeline.adoc` were taken, and it is the right granularity - a component's score is actionable, a whole-app average is not
- **`plugins` must list `@stryker-mutator/vitest-runner` explicitly.** Under pnpm's strict `node_modules` Stryker cannot discover it by convention and fails with "no TestRunner plugins were loaded", which reads like a missing install rather than a resolution problem
- **`thresholds.break` is `0`**, same as the backend config, so a low score always exits 0. Do not add a break threshold
- **Why not an incremental per-PR gate**, given StrykerJS supports `--since`: analysed and rejected in #2152. The short version is that per-diff mutation scores are noisy on small diffs, the run would add minutes to the very critical path `docs/TDRs/2_slow_ci_pipeline.adoc` exists to protect, and a score that gates merges invites killing mutants rather than testing behaviour

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
- `.github/PULL_REQUEST_TEMPLATE.md` - prompts for What/Why, the issue link, and a Testing section

## Notes

- Path filters prevent unnecessary builds (backend change → only `dotnet.yml` runs)
- Keycloak shares the same unified tag as backend/frontend; the upstream Keycloak version is tracked separately as an OCI label (`org.opencontainers.image.base.version`), extracted from `keycloak/Dockerfile`'s `FROM` line
- GitHub Pages deployment uses `permissions: pages: write, id-token: write` with concurrency group to cancel stale deployments
