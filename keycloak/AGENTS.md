# Keycloak - Identity & Access Management

## Overview

Custom Keycloak 26.7.0 image with the `einsatzbereit` realm pre-baked. Built and published to GHCR via `.github/workflows/keycloak.yml`.

```
keycloak/
├── Dockerfile              Multi-stage build (builder + optimized runtime)
├── README.md               Runtime env vars documentation
└── realms/
    └── einsatzbereit-realm.json    Realm config - source of truth for auth setup
```

## Realm Configuration

**File:** `realms/einsatzbereit-realm.json`  
Imported on container startup. This file IS the auth configuration - edit here, not in the Keycloak UI (UI changes don't persist across container restarts in dev).

### Roles (realm-level)

| Role | Purpose |
|---|---|
| `user` | Default - can browse opportunities |
| `organisator` | Can create and manage volunteer opportunities |
| `admin` | Full admin access - composite role, includes `user` + `organisator` so admin tokens also satisfy `EinsatzbereitDefaultUserPolicy`/`EinsatzbereitOrganisatorPolicy` |

### Clients

**`frontend`** (public OIDC client)
- Authorization Code + PKCE (S256 enforced) flow only
- ROPC disabled (`directAccessGrantsEnabled: false`) - use `frontend-test` for integration tests
- Redirect URIs: `http://localhost:*`, `https://einsatzbereit.maik-hasler.de/callback`
- Protocol mappers:
  - `realm-roles` - injects `roles: [...]` into id_token, access_token, userinfo
  - `realm-name` - injects hardcoded claim `realm: "einsatzbereit"` (used by backend auth policies)
  - `backend-audience` - adds `backend` client to audience in access tokens

**`frontend-test`** (public OIDC client, integration tests only)
- ROPC enabled (`directAccessGrantsEnabled: true`) - used by `IntegrationTestFixture.GetAccessTokenAsync`
- Redirect URIs: `http://localhost:*` only (never production)
- Same protocol mappers as `frontend` (roles, realm-name, backend-audience)

**`backend`** (confidential service account)
- Client secret: `backend-secret` for local Aspire dev/tests (`AppHost.cs` overwrites whatever is in the realm JSON before import, so the checked-in value is irrelevant there). On staging/production the realm JSON's `secret` is instead the placeholder `${KEYCLOAK_BACKEND_SECRET}` - Keycloak's realm import resolves `${VAR}` placeholders in any JSON value from an env var of the same name at container startup - and `docker-compose.yml` sources that env var from the `KEYCLOAK_BACKEND_SECRET` GitHub Environment secret (see `.github/workflows/publish.yml`), not a committed literal that would be usable against production the moment it lands in git history
- No user login flows - server-to-server only
- Service account permissions: `view-realm`, `manage-users`, `manage-organizations` - deliberately not `manage-realm` (full realm-admin), which the backend never needs and would let a leaked secret reconfigure clients, auth flows, and other realm settings
- Used by `KeycloakOrganizationService` in the backend to manage org membership

### Test Users

| Username | Password | Roles |
|---|---|---|
| `vera` | `vera123` | `user` |
| `olaf` | `olaf123` | `user`, `organisator` |
| `admin` | `admin123` | `admin` |

These credentials are stored **pre-hashed** (PBKDF2-SHA256) in the realm file, not as plaintext `value`. The realm's `passwordPolicy` (`upperCase(1)`, `length(8)`) rejects these short dev passwords, and Keycloak validates plaintext credentials against the policy during `--import-realm` - a fresh import (CI, a clean local stack, a first-time deploy) crashes with `invalidPasswordMinUpperCaseCharsMessage` and never starts. Pre-hashed credentials skip that validation, so **do not** replace them with plaintext `value` fields. To rotate one: import the realm, set the password in the UI, then partial-export the user.

Because the same realm file ships in the published Keycloak image, these accounts (including `admin`) are also reachable on the public staging deployment with the exact passwords above, and `OVERWRITE_EXISTING` (see `docker-compose.yml`) recreates them on every restart even if someone changes or deletes them there. This is intentional, not an oversight: staging is disposable demo/QA infrastructure, not production, and gets fully wiped on demand via `.github/workflows/reset-staging.yml`. Do not "fix" this by removing the accounts from the shipped image or by trying to give staging its own secret password without checking with the repo owner first - see #1166.

### Organizations Feature

Keycloak organizations are enabled (`"organizationsEnabled": true`). The backend delegates all org membership management to Keycloak - organizations are **not** duplicated in the application database.

## Docker Image

Multi-stage Dockerfile:
1. **Builder stage**: `quay.io/keycloak/keycloak:26.7.0` - runs `kc.sh build` with PostgreSQL provider
2. **Runtime stage**: Copies optimized build, runs with `--optimized` flag

Required environment variables at runtime (see `README.md`):
- `KC_HOSTNAME` - public hostname
- `KC_DB_URL` - JDBC connection string for Keycloak's own DB
- `KC_DB_USERNAME` / `KC_DB_PASSWORD`

The Aspire AppHost (`backend/src/Aspire/AppHost/AppHost.cs`) launches Keycloak with `KC_DB=dev-file` for local dev - Keycloak owns its own embedded H2 store there. The shared Postgres container hosts only the application `einsatzbereit` database.

## Updating the Realm

1. Make changes in the running Keycloak UI at http://localhost:8080
2. Export the realm: Admin UI → Realm Settings → Action → Partial Export (include clients, groups, roles)
3. Replace `realms/einsatzbereit-realm.json` with the export
4. Restart the Keycloak container to verify the import works

## Release Tagging

Tag format: `keycloak/vX.Y.Z.W` (4-part semver matching Keycloak's version scheme).  
`-rc.N` suffix = release candidate (published but not tagged `latest`).
