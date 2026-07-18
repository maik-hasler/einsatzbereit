# Reference

Stable reference material - the conventions the architecture tests enforce, the frontend stack and its lint gaps, the Keycloak realm, and a pointer to the formal ADRs/TDRs.

- [Backend conventions the architecture tests enforce](backend-conventions.md) - Clean Architecture layers, the Result-at-the-boundary rule, auto-discovery naming, per-endpoint rate limiting, and the test stack - most of it CI-enforced by NetArchTest.
- [Frontend stack, layout, and the quality gaps CI cannot catch](frontend-conventions.md) - React 19 + React Router v8 + Tailwind 4 via the Vite plugin, by-kind directory layout, and the a11y/i18n patterns no lint rule enforces.
- [Index of formal ADRs and TDRs](adr-tdr-index.md) - Pointer to the reviewed architecture decisions and technical-debt records under docs/ - link these, do not restate them in the wiki.
- [Keycloak realm: clients, mappers, users, and source of truth](keycloak-realm-config.md) - The realm JSON is the auth config; it defines three OIDC clients, the token mappers backend auth depends on, the seeded test users, and how organizations are split between Keycloak and the app database.
