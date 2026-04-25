# Keycloak — Identity & Access Management

## Overview

Custom Keycloak 26.6.1 image with the `einsatzbereit` realm pre-baked. Built and published to GHCR via `.github/workflows/keycloak.yml`.

```
keycloak/
├── Dockerfile              Multi-stage build (builder + optimized runtime)
├── README.md               Runtime env vars documentation
└── realms/
    └── einsatzbereit-realm.json    Realm config — source of truth for auth setup
```

## Realm Configuration

**File:** `realms/einsatzbereit-realm.json`  
Imported on container startup. This file IS the auth configuration — edit here, not in the Keycloak UI (UI changes don't persist across container restarts in dev).

### Roles (realm-level)

| Role | Purpose |
|---|---|
| `user` | Default — can browse opportunities |
| `organisator` | Can create and manage volunteer opportunities |
| `admin` | Full admin access |

### Clients

**`frontend`** (public OIDC client)
- Authorization Code + PKCE flow
- Direct access grants enabled (password flow for integration tests)
- Redirect URIs: `http://localhost:*`
- Protocol mappers:
  - `realm-roles` — injects `roles: [...]` into id_token, access_token, userinfo
  - `realm-name` — injects hardcoded claim `realm: "einsatzbereit"` (used by backend auth policies)

**`backend`** (confidential service account)
- Client secret: `backend-secret`
- No user login flows — server-to-server only
- Service account permissions: `manage-realm`, `manage-users`, `manage-organizations`
- Used by `KeycloakOrganizationService` in the backend to manage org membership

### Test Users

| Username | Password | Roles |
|---|---|---|
| `hannah` | `hannah123` | `user` |
| `olaf` | `olaf123` | `user`, `organisator` |
| `admin` | `admin123` | `admin` |

### Organizations Feature

Keycloak organizations are enabled (`"organizationsEnabled": true`). The backend delegates all org membership management to Keycloak — organizations are **not** duplicated in the application database.

## Docker Image

Multi-stage Dockerfile:
1. **Builder stage**: `quay.io/keycloak/keycloak:26.6.1` — runs `kc.sh build` with PostgreSQL provider
2. **Runtime stage**: Copies optimized build, runs with `--optimized` flag

Required environment variables at runtime (see `README.md`):
- `KC_HOSTNAME` — public hostname
- `KC_DB_URL` — JDBC connection string for Keycloak's own DB
- `KC_DB_USERNAME` / `KC_DB_PASSWORD`

The Aspire AppHost (`backend/src/Aspire/AppHost/AppHost.cs`) launches Keycloak with `KC_DB=dev-file` for local dev — Keycloak owns its own embedded H2 store there. The shared Postgres container hosts only the application `einsatzbereit` database.

## Updating the Realm

1. Make changes in the running Keycloak UI at http://localhost:8080
2. Export the realm: Admin UI → Realm Settings → Action → Partial Export (include clients, groups, roles)
3. Replace `realms/einsatzbereit-realm.json` with the export
4. Restart the Keycloak container to verify the import works

## Release Tagging

Tag format: `keycloak/vX.Y.Z.W` (4-part semver matching Keycloak's version scheme).  
`-rc.N` suffix = release candidate (published but not tagged `latest`).
