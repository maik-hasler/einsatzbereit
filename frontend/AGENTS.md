# Frontend - Vite + React 19 + Tailwind CSS 4

## Architecture

Vite SPA. React Router v8 for routing. Client-side OIDC via `react-oidc-context`. Static files served by nginx in production.

```
src/
├── client/
│   ├── api-client.ts       NSwag-generated TypeScript client - DO NOT HAND-EDIT
│   └── api-instance.ts     Creates EinsatzbereitApi with Bearer token (accepts optional token string)
├── hooks/
│   └── useApiClient.ts     React hook: returns api-client instance with token from useAuth()
├── components/
│   ├── Header/             Header with auth state (login/logout buttons, org switcher, notifications) + optional per-page breadcrumb/action bar (see `breadcrumb` prop) - split across Header.tsx (orchestrator), DesktopHeader.tsx, MobileHeader.tsx, MobileMenu.tsx, AccountControls.tsx, NotificationDropdown.tsx, NotificationItem.tsx, OrganizationSwitcher.tsx, LanguageSelector.tsx, BreadcrumbBar.tsx - all exclusive to Header, nothing here is imported outside this folder
│   ├── Footer.tsx          Footer with links and social icons
│   ├── CreateOrganizationModal.tsx     Modal form for org creation
│   ├── VolunteerOpportunitiesList/     Paginated list (size=10), filter bar - split across VolunteerOpportunitiesList.tsx (orchestrator), icons.tsx, MiniCalendar.tsx, FilterDropdown.tsx, OpportunityListItem.tsx, OpportunityResultsList.tsx, useVolunteerOpportunitiesData.ts, useCitySuggestions.ts - all exclusive to this folder, nothing here is imported outside it
│   └── CreateVolunteerOpportunityModal.tsx  Modal form for opportunity creation
├── layouts/
│   ├── AppLayout.tsx       Header + <Outlet /> + Footer
│   └── ProtectedRoute.tsx  Redirects to Keycloak if not authenticated
├── pages/
│   ├── HomePage.tsx                    Main page with VolunteerOpportunitiesList
│   ├── app/OrgDashboardPage/           Customizable widget dashboard (calendar, to-do, quick check-in, etc. - see widgetCatalog.ts) - org app shell's landing tab
│   ├── AdministrationPage.tsx          Platform-admin only: list/verify organizations, list users, toggle admin/enabled status
│   ├── PrivacyPolicyPage.tsx           Privacy policy (static)
│   └── ImprintPage.tsx                 Legal notice (static)
├── styles/global.css       Tailwind directives + custom brand theme
├── main.tsx                Entry point: AuthProvider + BrowserRouter + App
├── App.tsx                 React Router route declarations
└── vite-env.d.ts           ImportMetaEnv types for VITE_ variables
```

## Auth Flow

```
User clicks "Anmelden"
→ auth.signinRedirect() (react-oidc-context)
→ Keycloak login (PKCE, handled by oidc-client-ts)
→ Redirect back to /callback
→ AuthProvider processes code exchange, fires onSigninCallback (strips params)
→ auth.isAuthenticated = true, auth.user.access_token available
→ ProtectedRoute renders children
```

- `auth.user?.profile` - decoded id_token claims (sub, email, name, preferred_username, roles)
- `auth.user?.access_token` - Bearer token for API calls
- `active-org` cookie - last-opened organization id (set by `OrgAppLayout` on every successful org load; read by `HomePage`'s org-app resolution to skip straight to the right dashboard, see `lib/activeOrg.ts`)
- Roles: `auth.user?.profile?.roles` - flat string array from Keycloak custom mapper

## API Client

`src/client/api-client.ts` is auto-generated from `backend/src/Api/wwwroot/openapi-v1.json` by NSwag on every backend build. Never edit it manually - changes will be overwritten.

Use `useApiClient()` hook in all components:

```ts
const api = useApiClient();
await api.getVolunteerOpportunities(page, 10);
await api.createOrganization({ name });
```

For one-off calls outside React (e.g., scripts), use `createApiClient(token)` directly.

For endpoints with many optional query params (e.g. `getVolunteerOpportunities`, 17 positional params), don't call the generated client method directly - write a named-options wrapper instead, see `lib/volunteerOpportunities.ts`'s `fetchVolunteerOpportunities`.

## Environment Variables

Defined in `.env.development`. Exposed client-side via Vite (must use `VITE_` prefix).

| Variable                      | Dev value                                    |
| ----------------------------- | -------------------------------------------- |
| `VITE_KEYCLOAK_AUTHORITY_URL` | `http://localhost:8080/realms/einsatzbereit` |
| `VITE_KEYCLOAK_CLIENT_ID`     | `frontend`                                   |
| `VITE_API_URL`                | `http://localhost:5000`                      |

Accessed via `import.meta.env.VITE_*`.

## Role Checks

Roles come from `auth.user?.profile?.roles` (flat string array, custom Keycloak mapper).

```tsx
const roles = (
	Array.isArray(auth.user?.profile?.roles) ? auth.user!.profile.roles : []
) as string[];
const isOrganisator = roles.includes("organisator");
```

Known roles: `user`, `organisator`, `admin`.

## Routing

Routes declared in `src/App.tsx`. Add new routes there.

```tsx
// Public page
<Route path="/my-page" element={<MyPage />} />

// Protected page (requires login)
<Route path="/secure" element={<ProtectedRoute><SecurePage /></ProtectedRoute>} />
```

**Note:** New API methods become available in `useApiClient()` only after running `dotnet build` in `backend/` (NSwag regenerates `src/client/api-client.ts`). During development, new page code may use `(api as any)` until the client is regenerated.

## Scripts

```bash
pnpm dev             # dev server on :4321
pnpm build           # build to dist/ (static files)
pnpm preview         # preview production build
pnpm check           # tsc --noEmit
pnpm test            # vitest run - CI hard gate (frontend.yml's test job)
pnpm test:watch      # vitest in watch mode, for local development
pnpm test:coverage   # vitest run --coverage
pnpm lint            # eslint, zero warnings allowed
pnpm format:write    # apply Prettier formatting - run before every commit
pnpm format:check    # check Prettier formatting (used by CI)
pnpm i18n:check      # verify en.json/de.json key parity - CI hard gate, run before committing locale changes
```

## Unit Tests

Vitest (`vitest.config.ts`, jsdom environment) tests pure logic in `src/lib/`, colocated as `*.test.ts` next to the module under test (e.g. `src/lib/activeOrg.test.ts`). Component/page-level behavior is covered by the Playwright suite in `backend/tests/VisualTests/` instead - see root `AGENTS.md`.

Conventions used across the existing suite:

- Mock a module's own dependencies with `vi.mock(...)`, not the module under test itself. Values referenced inside a `vi.mock` factory must go through `vi.hoisted(...)` (mock factories are hoisted above imports, so a plain top-level `const` isn't initialized yet when the factory runs).
- Prefer computing the expected value with the same underlying call (e.g. `new Date(iso).toLocaleString(...)`) over hardcoding a formatted string, when the result depends on the host timezone or locale data.
- For module-level singletons/config computed at import time (e.g. `lib/runtimeConfig.ts`, `lib/keycloakRegistration.ts`), call `vi.resetModules()` in `beforeEach` and re-`import()` the module inside each test to get a fresh instance.
- `vi.spyOn` on an already-spied method (e.g. `console.error` spied in a previous test) returns the *same* mock and keeps its call history - restore with `vi.restoreAllMocks()` in `afterEach` rather than only resetting the fake in `beforeEach`.

## Key Dependencies

| Package              | Purpose                              |
| -------------------- | ------------------------------------ |
| `vite`               | Build tool + dev server              |
| `react` 19           | UI framework                         |
| `react-router` v8    | Client-side routing                  |
| `react-oidc-context` | Keycloak OIDC (wraps oidc-client-ts) |
| `oidc-client-ts`     | PKCE flow, token management          |
| `@tailwindcss/vite`  | Tailwind CSS 4 via Vite              |
| `leaflet`            | Map renderer (OpenStreetMap tiles)   |
| `react-leaflet`      | React bindings for Leaflet           |

## Linting

Always run `pnpm format:write` before committing. The CI `lint` job runs `format:check` and will fail if any Prettier violations exist - causing `build` to be skipped and a follow-up fix commit.

Run lint before every commit. All errors must be fixed - zero warnings allowed (`--max-warnings 0`).

```bash
pnpm lint
```

Rules enabled: `@typescript-eslint/strict`, `react-hooks/rules-of-hooks`, `react-hooks/exhaustive-deps`, `jsx-a11y/recommended`.

- No non-null assertions (`!`). Use `as Type` or type narrowing (`if (!x) return`).
- If `api` is intentionally excluded from `exhaustive-deps`, suppress with `// eslint-disable-next-line react-hooks/exhaustive-deps` and keep it consistent with the existing pattern.

## Accessibility (a11y)

`eslint-plugin-jsx-a11y` is enabled with the recommended ruleset. All violations are errors - CI will fail on any a11y lint issue.

Key conventions:

- **Modal dialogs**: Use the backdrop-button pattern. Separate the clickable backdrop (`<button aria-hidden="true" tabIndex={-1}>`) from the dialog container (`<div role="dialog" aria-modal="true" aria-labelledby="...">`) inside a neutral wrapper div. Handle Escape via `useEffect` on `document`.
- **Clickable cards**: Use a stretched `<Link className="absolute inset-0">` inside a `relative` `<li>` rather than putting `onClick` on the `<li>` directly. Any secondary links inside the card get `relative z-10` to sit above the stretched link.
- **Interactive elements**: Only use native interactive elements (`<button>`, `<a>`, `<input>`, etc.) for interactions. Never add `onClick` to non-interactive elements (`div`, `span`, `li`, etc.) without an appropriate ARIA role.
- **Images**: All `<img>` tags need an `alt` attribute. Purely decorative images use `alt=""`.
- **SVG icons**: Decorative SVGs get `aria-hidden="true"`. Meaningful standalone SVGs need a `<title>` or `aria-label`.
- **Form labels**: Every form control must have an associated `<label htmlFor="...">` or `aria-label`.
- **`<a href="#">`**: Never use `href="#"`. Use a `<button>` if there is no navigation target.

Automated axe-core checks run in the Playwright visual tests (`backend/tests/VisualTests/AccessibilityTests.cs`) on every major page and several stateful views (edit mode, modals, widget dialogs) - grep that file for `HasNoSeriousA11yViolations` for the current, authoritative list rather than trusting a copy of it here. Tests fail on any "serious" or "critical" axe violation. A new page/route needs a matching test in that file - `a11y-check` flags a missing one.

## Production

Static files in `dist/` served by nginx. `nginx.conf.template` handles SPA routing via `try_files $uri /index.html`; `docker-entrypoint.d/99-runtime-config.sh` renders it (and `config.js`) into their final form at container start via `envsubst`, filling in the Content-Security-Policy's `connect-src`/`frame-src` origins from the same `VITE_API_URL`/`VITE_KEYCLOAK_AUTHORITY_URL` env vars used for runtime config, plus `img-src`'s MinIO storage origin from a separate `STORAGE_PUBLIC_URL` env var (matching the backend's `Storage__PublicEndpoint` - uploaded org logos/opportunity banners/avatars are served from there, not the API origin), so one image works across environments. The CSP string itself is defined once via an nginx `map` (`$csp_header`) and referenced from all four location blocks - `frontend/scripts/check-nginx-csp.js` (`pnpm check:nginx-csp`, wired into CI) guards against a location block silently falling out of sync with that definition.

**Important:** CORS must be configured on the backend to allow the frontend origin, since API calls are now cross-origin (no server-side proxy).
