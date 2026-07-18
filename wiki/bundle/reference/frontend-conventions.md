---
type: "reference"
title: "Frontend stack, layout, and the quality gaps CI cannot catch"
description: "React 19 + React Router v8 + Tailwind 4 via the Vite plugin, by-kind directory layout, and the a11y/i18n patterns no lint rule enforces."
tags:
  - frontend
  - a11y
  - i18n
  - nswag
  - persona
timestamp: 2026-07-18
---

# Stack

Vite SPA. React 19, React Router v8 (`package.json` pins `react-router` 8.2.0, though `frontend/AGENTS.md` still says v7), client-side OIDC via `react-oidc-context` (wrapping `oidc-client-ts`, PKCE). Routes are declared centrally in `src/App.tsx`; there is no file-based routing. Pages that require login are wrapped inline in `<ProtectedRoute>` (from `src/layouts/`), which redirects to Keycloak when unauthenticated. Roles arrive as a flat string array at `auth.user?.profile?.roles` (custom Keycloak mapper); known values are `user`, `organisator`, `admin`. Access token is `auth.user?.access_token`.

Tailwind CSS 4 is wired through the `@tailwindcss/vite` plugin, called in `vite.config.ts`. There is **no `tailwind.config.js` and no `postcss.config.js`** - an agent carrying Tailwind 3 habits will look for a config file, a `content` glob, or a PostCSS chain and find none. Theme and brand tokens live in `src/styles/global.css`. Do not add a config file to "fix" missing utilities; extend the CSS.

# Directory layout by kind

`src/` is organized by kind, not by feature: `client/` (the NSwag-generated `api-client.ts` plus its instance factory), `hooks/`, `components/`, `layouts/`, `pages/`, `contexts/`, `lib/`, `locales/`, `styles/`. Org-scoped pages live under `pages/app/` (dashboard, members, settings, engagements), reached through the protected `/organizations/:organizationId/...` route group.

A component grows into a folder only when it gets complex. The create-opportunity wizard is the model: `components/CreateVolunteerOpportunityModal/` holds `index.tsx` as the container, one file per wizard step (`BasicsStep.tsx`, `DetailsStep.tsx`, `FormatStep.tsx`, `LocationStep.tsx`), a colocated `schema.ts` for validation, and `shared.tsx`. Follow that shape rather than a single 700-line file.

# The a11y patterns lint cannot catch

`eslint-plugin-jsx-a11y` runs the recommended ruleset as CI-blocking errors, and axe-core runs in the Playwright visual tests. Both check per-element facts (an `<img>` has `alt`, a control has a label, no `onClick` on a bare `div`). Neither can verify a cross-element structural pattern, so two conventions are on you:

- **Modal dialogs use the backdrop-button pattern.** The clickable backdrop is a separate `<button aria-hidden="true" tabIndex={-1}>`, and the dialog is a sibling `<div role="dialog" aria-modal="true" aria-labelledby="...">`, both inside a neutral wrapper. Escape-to-close is wired in a `useEffect` listening on `document`. Lint sees two valid elements and cannot tell whether they are composed correctly or whether Escape is handled at all.
- **Clickable cards use a stretched Link, never an `onClick` on the `<li>`.** Put `<Link className="absolute inset-0">` inside a `relative` `<li>`; any secondary link inside the card gets `relative z-10` to sit above the stretched link. Moving the click to the `<li>` would be flagged, but a card built the wrong way with a nested button still passes lint while breaking keyboard and screen-reader use.

# Announcing publish-blocking errors

A validation error that blocks a submit must do more than render. In the opportunity wizard (#689), the "needs a time slot" publish error was a plain `<p>` below the fold of the modal's scrollable body, so Publish looked like it did nothing. The fix: give the error `role="alert"` plus a `useEffect` that scrolls and focuses it into view. The effect is keyed on both the error string and a **submit-attempt counter** (`errorToken`) bumped on every failing attempt, including a retry that produces the identical message. Keying on the string alone would not re-fire when the message did not change, so a user clicking Publish twice would get no second announcement. Reuse this counter pattern for any repeatable blocking error.

# i18n has no key-parity check

Translations live in `locales/en.json` and `locales/de.json`, both nested under a top-level `translation` key, wired via i18next in `src/i18n.ts` (`fallbackLng: "en"`). Nothing in CI checks that the two files have the same keys. A key present in `en.json` but missing from `de.json` does not fail the build - i18next silently falls back to the English string and ships German-with-English-holes. When you add a key, add it to both files in the same change. The repo's `i18n-check` agent exists specifically because no lint rule covers this.

# Keycloak registration needs its own UserManager

The Register button must not call `auth.signinRedirect()` - that hits Keycloak's login form (#698). Keycloak exposes a separate registration endpoint that accepts the same OIDC params and completes with the same authorization-code callback. `frontend/src/lib/keycloakRegistration.ts` builds a dedicated `UserManager` that overrides only `authorization_endpoint` (via `metadataSeed`, which wins over the fetched discovery document) to point at `/protocol/openid-connect/registrations`, while sharing the app's localStorage-backed state/user stores so `/callback` still validates the result. Call `signinRedirectForRegistration()` for Register, `auth.signinRedirect()` for Sign in.

# Related

- [nswag-generated-clients](/gotchas/nswag-generated-clients.md) - api-client.ts is consumed via useApiClient() and must not be hand-edited
- [claude-check-setup](/decisions/claude-check-setup.md) - the a11y-check and i18n-check agents exist precisely to cover these lint gaps
- [pre-launch-testing-event](/project/pre-launch-testing-event.md) - the 'unfamiliar with technology' role card exercises exactly these a11y patterns

# Citations

- frontend/AGENTS.md:1-33
- frontend/AGENTS.md:149-163
- frontend/src/i18n.ts
- #689
- #698
