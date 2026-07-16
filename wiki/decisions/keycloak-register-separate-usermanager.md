---
type: decision-note
title: Route Keycloak's Register button through a second UserManager with a metadataSeed override
description: Sign in and Register both called the same signinRedirect(), always landing on Keycloak's login form; a second oidc-client UserManager overriding just the authorization_endpoint sends Register to Keycloak's registration page instead.
tags: [frontend, keycloak, oidc]
timestamp: 2026-07-16
---

# Schema

To send a user to a different page on the same OIDC provider (e.g. registration instead of login) without building a second auth flow, a second `UserManager` instance can override a single metadata field - here, `authorization_endpoint` - while still sharing the same client id, redirect URI, and local-storage-backed state/user store as the default `UserManager`, so the callback validates normally either way.

# Examples

The header's Sign in and Register buttons both called `auth.signinRedirect()` with identical parameters, so Keycloak had no signal to distinguish login intent from registration intent - Register always landed on the login form. The fix, `frontend/src/lib/keycloakRegistration.ts`, adds a second `UserManager` whose `metadataSeed` overrides only `authorization_endpoint` to target `/realms/{realm}/protocol/openid-connect/registrations`.

A related follow-up extended the same `UserManager` setup to pass `ui_locales` on every `signinRedirect()` call and read the locale claim back off the ID token in `onSigninCallback`, so the Keycloak page's language stays in sync with the app's i18next language in both directions.

# Citations

- `#692` https://github.com/maik-hasler/einsatzbereit/issues/692, fixed by commit `24dc07c` (#698)
- `#690` https://github.com/maik-hasler/einsatzbereit/issues/690, fixed by commit `c3ee01f` (#699)
- `frontend/src/lib/keycloakRegistration.ts`
