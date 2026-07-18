---
type: "reference"
title: "Keycloak realm: clients, mappers, users, and source of truth"
description: "The realm JSON is the auth config; it defines three OIDC clients, the token mappers backend auth depends on, the seeded test users, and how organizations are split between Keycloak and the app database."
tags:
  - keycloak
  - authorization
  - adr
  - playwright
timestamp: 2026-07-18
---

# Keycloak realm config

`keycloak/realms/einsatzbereit-realm.json` is imported on container startup and IS the auth configuration. There is no separate persisted store to reconcile against: local Aspire runs Keycloak with `KC_DB=dev-file` (embedded H2), so Keycloak owns its own store and the shared Postgres container holds only the application `einsatzbereit` database. Changes made in the Keycloak admin UI do not survive a restart. To make a durable change: edit in the running UI, partial-export the realm (include clients, groups, roles), replace the JSON with the export, then restart the container to confirm the import works. Editing the JSON by hand for anything non-trivial is error prone; round-trip through the UI export.

# The three clients

There are three OIDC clients, and picking the wrong one for a task is the common mistake.

- **frontend** (public): Authorization Code + PKCE with S256 enforced, and nothing else. ROPC is off (`directAccessGrantsEnabled: false`). This is the real app client.
- **frontend-test** (public): identical mappers to `frontend` but with ROPC enabled (`directAccessGrantsEnabled: true`). It is the password-grant client for the test suites - `IntegrationTestFixture.GetAccessTokenAsync` and the many VisualTests that request their own token both use it. Its redirect URIs are `http://localhost:*` only, never production. Do not enable ROPC on `frontend` to shortcut a test; use this client.
- **backend** (confidential service account): no user login flow, server-to-server only. Its service account holds `manage-realm`, `manage-users`, and `manage-organizations`, and `KeycloakOrganizationService` uses it. Dev secret is `backend-secret`; staging injects it via `KEYCLOAK_BACKEND_SECRET`.

# The mappers backend auth depends on

Both frontend clients carry the same three protocol mappers, and two of them are load-bearing for API calls:

- **realm-roles** injects `roles: [...]` into the id_token, access_token, and userinfo.
- **realm-name** injects a hardcoded claim `realm: "einsatzbereit"`.
- **backend-audience** adds the `backend` client to the access token audience.

The non-obvious trap: backend auth policies reject any token missing the `realm` claim OR the `backend` audience. A token that Keycloak issues and considers valid can still be refused at the API, surfacing as a 403 or 404 rather than an obvious auth error. If a request fails auth despite a good login, check that the token carries both claims before suspecting the API code.

# Test users

Three users are seeded in the realm file:

| Username | Password | Roles |
|---|---|---|
| `vera` | `vera123` | `user` |
| `olaf` | `olaf123` | `user`, `organisator` |
| `admin` | `admin123` | `admin` |

Reach for **olaf** when a test needs organizer capabilities (creating and managing opportunities); `vera` only has the base `user` role.

# Organizations live in both Keycloak and the app database

Organizations are not delegated entirely to Keycloak. The realm enables Keycloak's organizations feature (`organizationsEnabled: true`), and `KeycloakOrganizationService` (through the `backend` service account's `manage-organizations` role) performs the Keycloak-side operations. But the application database also persists organizations: an `organization` table (migration `20260406074452_Initial`) and an `organization_membership` table (`20260715212802_AddOrganizationMembership`), backed by the `Organization` domain aggregate.

The split that matters at request time: whether a user is an organizer of a given org is answered from the local `organization_membership` table (`IApplicationDbContext.IsOrganizerAsync`, called by `OwnershipGuard.EnsureIsOrganizerAsync`), never by calling Keycloak on the hot path. Keycloak stays the source of truth for who is an organizer; the local table is a projection seeded from it once at startup. That seeding, and the lockout that hits every existing organizer when it is skipped on a fresh deploy, is the subject of auth-fresh-deploy-traps.

# Related

- [auth-fresh-deploy-traps](/gotchas/auth-fresh-deploy-traps.md) - the pre-hashed-password import crash is a property of this realm file
- [live-playwright-scripts](/process/live-playwright-scripts.md) - these test users and the login form structure are what the scripts drive
- [backend-conventions](/reference/backend-conventions.md) - backend auth policies consume the realm claim and audience defined here
- [adr-tdr-index](/reference/adr-tdr-index.md) - self-hosted Keycloak is ADR-3

# Citations

- keycloak/AGENTS.md:16-62
- keycloak/AGENTS.md:30-48
- keycloak/AGENTS.md:50-56
- backend/src/Application/Common/Persistence/IApplicationDbContext.cs - IsOrganizerAsync / GetOrganizerOrganizationsAsync
- backend/src/Infrastructure/Persistence/Migrations/20260406074452_Initial.cs - the organization table
- backend/src/Infrastructure/Persistence/Migrations/20260715212802_AddOrganizationMembership.cs - the organization_membership table
