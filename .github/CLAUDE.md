# .github — CI/CD & Issue Templates

## Workflows

```
.github/workflows/
├── dotnet.yml          Backend: build + test
├── frontend.yml        Frontend: lint → build → e2e tests
├── docs.yml            Docs: AsciiDoc build → GitHub Pages deploy
├── backend-publish.yml Backend: build/test → push Docker image to GHCR
├── frontend-publish.yml Frontend: lint/build/test → push Docker image to GHCR
└── keycloak.yml        Keycloak: push Docker image to GHCR
```

## CI Workflows (run on push/PR to main)

### `dotnet.yml`
- **Trigger:** `backend/**` path filter or manual
- **Steps:** restore → build → test
- **Working dir:** `backend/`

### `frontend.yml`
- **Trigger:** `frontend/**` path filter or manual
- **Jobs (sequential):** lint → build → tests
  - `lint`: `pnpm lint` + `pnpm check` (type check)
  - `build`: `pnpm build`
  - `tests`: Playwright (`pnpm test:tests`)

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
