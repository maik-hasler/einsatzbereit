# Keycloak - Identity & Access Management

## Overview

Custom Keycloak 26.7.1 image with the `einsatzbereit` realm pre-baked. Built and published to GHCR via the `publish-keycloak` job in `.github/workflows/publish.yml`; `.github/workflows/keycloak-realm-import.yml` guards that the committed realm still imports cleanly on that Keycloak version before it reaches staging.

```
keycloak/
├── Dockerfile              Multi-stage build (builder + optimized runtime)
├── README.md               Runtime env vars documentation
├── realms/
│   └── einsatzbereit-realm.json    Realm config - source of truth for auth setup
└── themes/einsatzbereit/login/     Custom login theme (see "Login Theme" below)
```

## Login Theme

**Directory:** `themes/einsatzbereit/login/`, selected by the realm's `loginTheme`. `parent=base`, so any template not overridden here falls through to Keycloak's own - which is how the pages a real signup walks through ended up rendering stock markup inside this theme's card (#1758).

The theme now overrides every template a visitor can reach with this realm's settings:

| Template | Reached by |
|---|---|
| `login.ftl` | sign-in |
| `register.ftl` | `/registrations` |
| `login-reset-password.ftl` | "forgot password" |
| `login-verify-email.ftl` | every registration (`verifyEmail: true`) |
| `login-update-password.ftl` | after email confirmation, and from the reset mail |
| `login-update-profile.ftl` | UPDATE_PROFILE required action |
| `terms.ftl` | TERMS_AND_CONDITIONS required action (not enabled today) |
| `info.ftl` / `error.ftl` | action-token outcomes, expired links |
| `login-page-expired.ftl` | an idle login form |
| `logout-confirm.ftl` | RP-initiated logout without an `id_token_hint` |

**There is no password field on the registration form, and that is Keycloak's design, not a bug.** With `verifyEmail: true`, `RegistrationPassword.buildPage` deliberately omits it and sets UPDATE_PASSWORD once the address is confirmed instead. `register.ftl` renders the password fields whenever `passwordRequired` *is* set, so turning `verifyEmail` off restores them - and `KeycloakThemeTests.Register_OmitsPasswordFields_AndSaysWhy` fails, which is the correct signal that the explanatory lead needs to go too.

Two constraints that are easy to trip over:

- **The `frontend` client has no `baseUrl`.** Base's `error.ftl`, `info.ftl` and `logout-confirm.ftl` all gate their only way out on `${client.baseUrl}`, so all three rendered with nothing to click. The overrides fall back to `properties.siteUrl` (declared in `theme.properties`) instead. Keep that fallback on any new template with an exit.
- **Keycloak's stock German is "Sie"; the product is "du".** Any base message that reaches a user has to be overridden in `messages/messages_de.properties` (and its English twin) or the funnel mixes both registers on one screen.

Colors, radii, shadows and the control recipes are mirrored from the frontend's `@theme` block and `lib/formClasses.ts` / `lib/surfaceClasses.ts` - change them there first, then here. `resources/img/logo.svg` and `favicon.svg` are byte-identical copies of `frontend/public/`; re-copy rather than hand-editing.

Covered by `backend/tests/VisualTests/KeycloakThemeTests.cs`, which drives Keycloak's origin directly and creates throwaway users to reach the required-action pages (`AspireFixture.CreateThrowawayUserAsync`). Not covered, and deliberately: the TOTP/WebAuthn/identity-provider/consent templates, none of which this realm can reach. They fall back to base markup, over the class-hook mappings in `theme.properties` and the fallback rules at the end of `einsatzbereit.css`, so they degrade to plain rather than to unstyled.

## Realm Configuration

**File:** `realms/einsatzbereit-realm.json`  
Imported on container startup. This file IS the auth configuration - edit here, not in the Keycloak UI (UI changes don't persist across container restarts in dev).

### Roles (realm-level)

| Role | Purpose |
|---|---|
| `user` | Default - can browse opportunities |
| `organisator` | Can create and manage volunteer opportunities |
| `admin` | Full admin access - composite role, includes `user` + `organisator` so admin tokens also satisfy `EinsatzbereitDefaultUserPolicy`/`EinsatzbereitOrganisatorPolicy` |

The realm's `defaultRole` (`default-roles-einsatzbereit`, composite over `user`) is what Keycloak grants automatically to every newly created user, including self-registrations through the public `/protocol/openid-connect/registrations` form (#1723 - without it, self-registered accounts got no realm role at all and every `EinsatzbereitDefaultUserPolicy`-gated endpoint 403'd for them). Deliberately does not also compose Keycloak's built-in `offline_access`/`uma_authorization`/account-client roles the way a full UI export would - this realm's import is a hand-authored partial file, and those built-ins are not yet provisioned at the point a partial import resolves `roles.realm` composites, so referencing them there fails `--import-realm` outright (confirmed via `keycloak-realm-import.yml` against the real production Keycloak version: `Unable to find composite realm role: uma_authorization`). None of the app's clients request the `offline_access`/`uma_authorization` scopes anyway, so nothing is lost by leaving them out. It does **not** apply to the three seeded test users above or `service-account-backend` - those are created via this file's `users` array during realm import, which bypasses Keycloak's normal user-creation code path and only grants the `realmRoles`/`clientRoles` listed explicitly on each entry.

### Clients

**`frontend`** (public OIDC client)
- Authorization Code + PKCE (S256 enforced) flow only
- ROPC disabled (`directAccessGrantsEnabled: false`) - use `frontend-test` for integration tests
- Redirect URIs / web origins / post-logout redirect URIs: `https://einsatzbereit.maik-hasler.de` only in the committed realm - the same file ships baked into the production Keycloak image (`Dockerfile`), so a `http://localhost:*` entry here would be live on `login.maik-hasler.de` (#1190). `AppHost.cs` overlays `http://localhost:*` (and `webOrigins: ["*"]`, since Aspire's dynamic port can't be matched by a fixed origin) back in for local Aspire/Playwright runs only - see its comment above the realm-patching block
- Protocol mappers:
  - `realm-roles` - injects `roles: [...]` into id_token, access_token, userinfo
  - `realm-name` - injects hardcoded claim `realm: "einsatzbereit"` (used by backend auth policies)
  - `backend-audience` - adds `backend` client to audience in access tokens

**`frontend-test`** (public OIDC client, integration tests only)
- `enabled: false` in the committed realm - it ships in the same realm baked into the staging/production image (#1167: a public client with ROPC enabled there turns credential stuffing into a single scriptable `grant_type=password` request, no browser, no PKCE, no redirect-URI constraint). `backend/src/Aspire/AppHost/AppHost.cs` flips it back to `enabled: true` in the dev-only realm copy it writes before import, since that's the only path that ever needs it live - see below
- ROPC enabled (`directAccessGrantsEnabled: true`) - used by `IntegrationTestFixture.GetAccessTokenAsync` and `VisualTests/AspireFixture.SignInAsync`, both of which boot Keycloak through the Aspire AppHost, never the baked image
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
1. **Builder stage**: `quay.io/keycloak/keycloak:26.7.1` - runs `kc.sh build` with PostgreSQL provider
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
