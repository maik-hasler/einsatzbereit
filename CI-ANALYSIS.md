# CI/CD Analysis: Playwright + Docker Stack

_Generated 2026-04-23 — branch `claude/fix-playwright-tests-PbEMy`_

---

## What broke and why

### Symptom
```
container keycloak is unhealthy
Error: Process completed with exit code 1.
```
`docker compose up -d --no-build --wait postgres keycloak backend frontend` exits non-zero when any service never transitions to `healthy`.

### Root cause 1 — Keycloak health check is unreliable in CI

**The health check in `docker-compose.yml`:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -sf http://localhost:9000/health/ready || exit 1"]
  interval: 10s
  timeout: 5s
  retries: 20
  start_period: 60s
```

Two problems:

1. **`curl` may not exist.** `quay.io/keycloak/keycloak:26.6.1` is based on `ubi9/ubi-micro`. This minimal image does not ship `curl`. The `CMD-SHELL` command itself errors before it can hit the endpoint.

2. **Start-up time underestimated.** Even if `curl` works, Keycloak in the current config must:
   - Connect to PostgreSQL (`KC_DB=postgres`)
   - Run schema migrations against the `keycloak` DB
   - Import the realm from `realms/einsatzbereit-realm.json` (users, clients, roles, org config)
   - Finish JVM warm-up on a shared CI runner

   In practice this takes 75–120 s on GitHub-hosted runners. The current `start_period: 60s` starts checking too early, and 20 × 10 s = 200 s total window may or may not be enough — it's a race.

### Root cause 2 — Backend has no health check, tests start too early

`docker-compose.yml` defines no `healthcheck` for `backend`. `docker compose up --wait` considers a service "done" as soon as the container is running (not exited) when no health check is configured. The backend runs EF Core migrations in Development mode on startup — this takes 5–15 s. Tests that hit the backend during migration get 500s or connection resets.

### Root cause 3 — Sequential lint → build → tests wastes ~15 s

```
lint (23 s) → build (15 s) → tests (5 m 13 s)
```

`lint` and `build` are independent jobs; neither uses the other's output. They can run in parallel, letting `tests` start ~15 s earlier.

### Root cause 4 — Playwright runs with 1 worker

`workers: 1` means all 13 spec files execute sequentially. Tests already use:
- Pre-authenticated storage states (`tests/.auth/olaf.json`, `tests/.auth/hannah.json`)
- `resetState()` between tests for DB isolation

This is compatible with 2 workers. Doubling workers reduces test time by ~40 %.

---

## Architecture observations

### Current flow (what's slow)

```
push → lint (23s)
             └→ build (15s)
                       └→ tests (5m 13s)
                           ├── Pull/load postgres:18 image    ~5s (cached)
                           ├── docker buildx bake ×3 images   ~60-90s (cached layers)
                           ├── docker compose up --wait        ~90-120s (Keycloak bottleneck)
                           ├── pnpm install                    ~15s
                           ├── playwright install (cached)     ~5s
                           └── 13 spec files × ~15s each       ~3m (1 worker)
```

Total wall-clock from push: ~6.5 minutes

### Target flow (after fixes)

```
push → lint (23s) ─┐
     → build (15s)─┴→ tests (~3-4m)
                        ├── postgres:18 cache load             ~5s
                        ├── docker buildx bake ×3              ~60-90s
                        ├── docker compose up --wait           ~35-45s  ← H2 Keycloak
                        ├── pnpm install                       ~15s
                        ├── playwright install                  ~5s
                        └── 13 spec files, 2 workers           ~90s
```

Total wall-clock from push: ~4.5 minutes

---

## Fixes applied

### Fix 1: `docker-compose.ci.yml` (new file)

Override Keycloak for CI to use H2 in-memory instead of PostgreSQL.

```yaml
services:
  keycloak:
    command: ["start-dev", "--import-realm"]
    environment:
      KC_DB: dev-mem
    healthcheck:
      start_period: 30s
      retries: 12
```

Why this works:
- `KC_DB: dev-mem` → H2 in-memory; no schema migration, no postgres round-trips
- `start-dev` → skips the Keycloak "production hardening" startup path; JVM starts faster
- `--import-realm` still works; realm JSON is still mounted via the same volume
- Keycloak is healthy in ~20-30 s instead of 75-120 s
- Postgres still starts (backend needs it), but Keycloak no longer blocks on it

Composed with the base file:
```bash
docker compose -f docker-compose.yml -f docker-compose.ci.yml up ...
```

### Fix 2: Backend health check in `docker-compose.yml`

```yaml
backend:
  healthcheck:
    test: ["CMD-SHELL", "wget -qO /dev/null http://localhost:8080/health || exit 1"]
    interval: 5s
    timeout: 10s
    retries: 20
    start_period: 30s
```

`wget` is in the .NET base image (`aspnet:10.0` on Debian/Ubuntu). Uses internal port 8080 (mapped to 5000 on host).

Requires adding `AddHealthChecks()` + `MapHealthChecks("/health")` in `backend/src/Api/Program.cs`.

Also tightens `frontend` depends_on:
```yaml
frontend:
  depends_on:
    backend:
      condition: service_healthy   # was: service_started (implicit)
    keycloak:
      condition: service_healthy   # was: service_started
```

### Fix 3: Parallelize lint + build in `frontend.yml`

Before:
```yaml
build:
  needs: lint    # ← sequential
tests:
  needs: build
```

After:
```yaml
build:
  # no needs → runs in parallel with lint
tests:
  needs: [lint, build]   # waits for both
```

Same change applied to `frontend-publish.yml`.

### Fix 4: Playwright workers: 2 in CI

```typescript
workers: process.env.CI ? 2 : 1,
```

Tests are isolated by storage state + `resetState()`. Two workers is safe.

---

## Files changed

| File | Type | Change |
|---|---|---|
| `docker-compose.ci.yml` | **NEW** | H2 Keycloak override for CI |
| `docker-compose.yml` | modified | Backend healthcheck + frontend depends_on |
| `backend/src/Api/Program.cs` | modified | `AddHealthChecks` + `MapHealthChecks("/health")` |
| `.github/workflows/frontend.yml` | modified | Parallel lint+build, CI compose override |
| `.github/workflows/frontend-publish.yml` | modified | CI compose override |
| `frontend/playwright.config.ts` | modified | `workers: 2` in CI |

---

## What was NOT changed and why

- **Keycloak `Dockerfile.integration`**: `start-dev` works fine with the existing optimized image. No rebuild needed.
- **Test code**: Tests are correctly isolated; no test logic changes needed.
- **Postgres caching**: Already well-implemented. Left as-is.
- **GHA layer cache (bake scopes)**: Already correct. Left as-is.
- **`retries`/`timeout` on Keycloak health check**: Reduced slightly because H2 is fast; base values remain reasonable as fallback.
