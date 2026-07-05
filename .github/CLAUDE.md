# .github - CI/CD & Issue Templates

## Workflows

```
.github/workflows/
├── dotnet.yml        Backend: build + test
├── frontend.yml      Frontend: lint → build
├── docs.yml          Docs: AsciiDoc build → GitHub Pages deploy
├── publish.yml       Tag-triggered: build + push backend/frontend/keycloak to GHCR, then deploy-staging
├── release-rc.yml    Promotes a release/v* branch into a real tag (used by Claude Code on the web)
├── lint.yml          Ban em/en dashes + EditorConfig check
└── pr-title.yml      Validate PR title against Conventional Commits
```

## CI Workflows (run on push/PR to main)

### `dotnet.yml`
- **Trigger:** `backend/**`, `frontend/**`, `keycloak/**` path filter or manual (frontend + keycloak included because VisualTests boots full stack via Aspire)
- **Steps:** setup .NET + Node + pnpm → frontend `pnpm install` → `dotnet restore` → `dotnet build` → run each test project sequentially via `dotnet run --project ... --no-build`
- **Test projects:** Application.UnitTests, ArchitectureTests, IntegrationTests, VisualTests
- **Why `dotnet run` not `dotnet test`:** TUnit uses Microsoft.Testing.Platform; `dotnet test` on .NET 10 requires opt-in to new experience. `dotnet run` invokes the test runner directly.
- **Typical duration:** ~20-30 minutes end to end, dominated by the `VisualTests` job (boots the full Aspire stack, then drives it with Playwright). When polling `get_check_runs` for this workflow, don't re-poll more often than every ~10 minutes while it's `in_progress` - shorter intervals just burn turns without new information.

### `frontend.yml`
- **Trigger:** `frontend/**` path filter or manual
- **Jobs (sequential):** lint → build
  - `lint`: `pnpm lint` + `pnpm check` (type check)
  - `build`: `pnpm build`
- **No E2E job** - E2E lives in backend `tests/VisualTests/` (run by `dotnet.yml`)

### `docs.yml`
- **Trigger:** `docs/**` path filter or manual
- **Jobs:** build AsciiDoc → deploy to GitHub Pages
- Uses `tonynv/asciidoctor-action` with `asciidoctor-diagram` for PlantUML

## Publish Workflows (tag-triggered)

Components are released independently with their own tags.

| Component | Tag pattern | Image name |
|---|---|---|
| Backend | `backend/vX.Y.Z` | `{repo}-backend` |
| Frontend | `frontend/vX.Y.Z` | `{repo}-frontend` |
| Keycloak | `keycloak/vX.Y.Z.W` | `{repo}-keycloak` |

All images pushed to **GitHub Container Registry (GHCR)**.

**Release candidates:** Append `-rc.N` to the tag (e.g., `backend/v1.0.0-rc.1`). Image is published but `latest` tag is NOT updated.

**Full release:** Tag without `-rc` suffix → image published + `latest` tag updated.

### Publish flow (backend/frontend)
1. Run full test suite (same as CI workflow)
2. Login to GHCR
3. Extract version from tag (strips component prefix)
4. Build and push Docker image
5. Tag with version + `latest` (if not RC)

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

## Issue Templates

```
.github/ISSUE_TEMPLATE/
├── bug_report.yml       [Bug]: prefix, label: bug
└── feature_request.yml  [Feature]: prefix, label: enhancement
```

Both templates are in **German**. Fields:

**Bug report:** Priorität (Niedrig/Mittel/Hoch), Beschreibung, Reproduktionsschritte, Zusätzliche Infos

**Feature request:** Priorität, User Story (Als X, möchte ich Y, damit Z), Akzeptanzkriterien (checkboxes), Beschreibung, Umsetzungsideen, Zusätzliche Infos

## Notes

- Path filters prevent unnecessary builds (backend change → only `dotnet.yml` runs)
- Keycloak version uses 4-part semver (`vX.Y.Z.W`) matching upstream Keycloak releases
- GitHub Pages deployment uses `permissions: pages: write, id-token: write` with concurrency group to cancel stale deployments
