# CI/CD Analysis: Playwright + Docker Stack

_Last revised 2026-04-23 — branch `claude/fix-playwright-tests-PbEMy`_

---

## Summary

The original setup tried to satisfy local development and CI from a single configuration. It didn't work — Keycloak started with `KC_DB=postgres` + runtime realm import, had a bash-only `/dev/tcp` healthcheck that failed on the `ubi-micro` base image, and CI papered over both problems with an overlay that swapped in `dev-mem` and `start-dev` while also fighting an `ENTRYPOINT` conflict in the Dockerfile. Locally, `docker compose up --build` exited with `Container keycloak  Error`.

The redesign separates concerns cleanly:

- **`docker-compose.yml`** owns local development (postgres-backed Keycloak, realm volume-mounted from the repo for quick edits).
- **`docker-compose.ci.yml`** owns CI — a small overlay that only swaps the Keycloak DB to `dev-mem`.
- **`keycloak/Dockerfile.integration`** defaults to `start-dev --import-realm`, so both local and CI use the same start command and the realm loads from the same JSON.
- **No Docker healthcheck** on Keycloak. Readiness is polled by Playwright's `global-setup.ts` against the OIDC discovery endpoint — a stronger signal than a TCP probe and no `curl`/`wget`/`/dev/tcp` bash problem on `ubi-micro`.

---

## Changes

### `keycloak/Dockerfile.integration` — simplified

```dockerfile
FROM quay.io/keycloak/keycloak:26.6.1

COPY realms/einsatzbereit-realm.json /opt/keycloak/data/import/einsatzbereit-realm.json

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start-dev", "--import-realm"]
```

- Single stage. No `kc.sh build` step (not needed for `start-dev`).
- Realm JSON baked into the image at the standard import path.
- `ENTRYPOINT`/`CMD` split so any compose override just uses `command:`.
- `start-dev` lets `KC_DB` be set at runtime to `postgres` (local dev) or `dev-mem` (CI) without rebuilding.

### `docker-compose.yml` — local development

Keycloak service:

- Uses `Dockerfile.integration`, `KC_DB=postgres` with the shared postgres container, realm volume-mounted at `/opt/keycloak/data/import` so edits to `einsatzbereit-realm.json` are picked up on next restart without a rebuild.
- No `command:` override — the Dockerfile CMD (`start-dev --import-realm`) is what runs.
- No healthcheck. Previously the block used `/dev/tcp` which doesn't work in `ubi-micro`; removing it removes a failure that wasn't giving any real signal.
- `depends_on: postgres (service_healthy)` retained — local dev still wants Keycloak's own DB on the shared postgres.
- `frontend.depends_on.keycloak` downgraded from `service_healthy` → `service_started` because no healthcheck exists any more.

Postgres, pgadmin, backend, and frontend blocks are unchanged. Backend already has a `wget`-based healthcheck at `/health` (`AddHealthChecks` + `MapHealthChecks("/health")` in `backend/src/Api/Program.cs`).

### `docker-compose.ci.yml` — CI overlay

```yaml
services:
  keycloak:
    environment:
      KC_DB: dev-mem
```

That's the full file. Everything else — build context, port mapping, `KC_BOOTSTRAP_ADMIN_*`, the realm volume mount — comes from the base compose. `KC_DB=dev-mem` shadows the postgres setting at runtime; `start-dev` accepts this. `KC_DB_URL`/`KC_DB_USERNAME`/`KC_DB_PASSWORD` from the base file are inherited but ignored by Keycloak when `KC_DB=dev-mem`.

The base file's `depends_on: postgres` is kept via inheritance. That's harmless in CI because postgres starts anyway for the application DB; Keycloak just waits a few seconds more than strictly necessary. Not worth `!reset` gymnastics to trim.

### `frontend/tests/global-setup.ts` — Keycloak readiness poll

The existing `waitFor()` helper is reused. One extra call added before the backend poll:

```ts
await waitFor(
  'http://localhost:8080/realms/einsatzbereit/.well-known/openid-configuration',
  'Keycloak',
);
```

The OIDC discovery endpoint returns 200 only once the realm is loaded and the HTTP listener is up — a strong readiness gate. The helper has a 120 s default timeout.

### `.github/workflows/frontend.yml` and `frontend-publish.yml`

Both files call out the overlay explicitly:

```yaml
- name: Start services
  run: docker compose -f docker-compose.yml -f docker-compose.ci.yml up -d --no-build --wait postgres keycloak backend frontend

- name: Dump container logs on failure
  if: failure()
  run: docker compose -f docker-compose.yml -f docker-compose.ci.yml logs --no-color
```

All other CI configuration (GHA bake cache scopes, postgres tar cache, Playwright browser cache, `COMPOSE_PROJECT_NAME=einsatzbereit`, parallel lint+build, 2 workers, failure log dump, HTML report upload) is unchanged.

### `.github/workflows/keycloak.yml` — unchanged

It builds `keycloak/Dockerfile` (production), not `Dockerfile.integration`. Production continues to use postgres-backed storage.

---

## Ownership

| File | Purpose |
|---|---|
| `docker-compose.yml` | Local development. Postgres-backed Keycloak. Realm volume-mounted for edit-and-restart loop. |
| `docker-compose.ci.yml` | CI overlay. Only `KC_DB: dev-mem` on Keycloak. |
| `keycloak/Dockerfile.integration` | Test/integration Keycloak image — used by both local dev and CI. `start-dev --import-realm` default. |
| `keycloak/Dockerfile` | Production Keycloak image. Unchanged. |
| `frontend/tests/global-setup.ts` | Readiness polling for Keycloak + backend before Playwright runs. |

---

## Startup flow

### Local dev (`docker compose up --build`)

1. postgres starts, becomes healthy (`pg_isready`).
2. `keycloak` starts, runs `kc.sh start-dev --import-realm`, imports realm JSON from the mounted volume, connects to the `keycloak` database on postgres. Ready in ~30–45 s.
3. backend starts, runs EF Core migrations, reaches `/health`. Healthy in ~5–15 s after start.
4. frontend starts once backend is healthy (nginx).

### CI (`docker compose -f docker-compose.yml -f docker-compose.ci.yml up --wait`)

1. postgres starts, becomes healthy.
2. `keycloak` starts, runs `kc.sh start-dev --import-realm` with `KC_DB=dev-mem`, imports realm into in-memory H2. Ready in ~15–20 s. No postgres round-trip for Keycloak.
3. backend + frontend as above.
4. Playwright `global-setup.ts` polls Keycloak's OIDC discovery endpoint, then the backend. Runs the suite with 2 workers.

---

## Risks and mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| `start-dev` is not production-supported. | N/A | This is the *integration* Keycloak image, not production. Production image (`keycloak/Dockerfile`) uses `start --optimized` with postgres. |
| `docker compose up --wait` no longer fails fast if Keycloak is broken at boot. | Medium | Playwright `global-setup.ts` throws `Keycloak did not become ready within timeout` after 120 s with a clear message. Slightly worse diagnostics than a compose `--wait` error; not lost. |
| Local dev Keycloak stores state in postgres — changes made via the admin UI persist across restarts (unlike CI's dev-mem). | None | Already expected. Documented in `keycloak/CLAUDE.md`: realm edits should round-trip through `einsatzbereit-realm.json` via Partial Export. |
| Realm JSON edits not picked up without restart. | Low | Mounted volume + `--import-realm` means `docker compose restart keycloak` picks up changes. Rebuild only required when the Dockerfile itself or the baked copy is authoritative (CI). |

---

## Verification

1. **Local** — `docker compose up --build`. All services reach `Up` (no healthchecks to trip on except backend+frontend). Browse `http://localhost:4321`, log in as `olaf / olaf123`.
2. **Local Playwright** — `cd frontend && pnpm test:e2e`. `global-setup.ts` polls Keycloak's discovery endpoint, suite runs green.
3. **CI** — push branch. `frontend.yml` starts the stack with the overlay; Playwright job green with 2 workers. Force a test failure once to confirm the log-dump step runs.
4. **Publish smoke** — optional: push `frontend/v0.0.0-rc.0`; `frontend-publish.yml` exercises the same path end-to-end.

---

## What was considered and rejected

- **Install `curl` in the integration image and keep a Docker healthcheck.** Adds a `microdnf` step, image weight, and a build-time network dependency for zero readiness benefit over the Playwright-side poll.
- **Bake the realm into a pre-populated H2 `dev-file` DB at build time.** Works, but requires `kc.sh build` + `kc.sh import` in a multi-stage Dockerfile and changes the start command to `start --optimized`. User preferred `start-dev` as the default for the integration image.
- **Merge CI into one config via `!reset` overlay tricks.** Compose 2.24+ supports `!reset`, but the current overlay is two lines. Not worth the cognitive cost.
- **Containerize Playwright.** Networking complexity against a Compose stack on the runner host, ~500 MB image pull. CI is always `ubuntu-latest`; host-installed + cached browsers is simpler and faster.
