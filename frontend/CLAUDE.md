# Frontend — Vite + React 19 + Tailwind CSS 4

## Architecture

Vite SPA. React Router v7 for routing. Client-side OIDC via `react-oidc-context`. Static files served by nginx in production.

```
src/
├── client/
│   ├── api-client.ts       NSwag-generated TypeScript client — DO NOT HAND-EDIT
│   └── api-instance.ts     Creates EinsatzbereitApi with Bearer token (accepts optional token string)
├── hooks/
│   └── useApiClient.ts     React hook: returns api-client instance with token from useAuth()
├── components/
│   ├── Header.tsx          Header with auth state (login/logout buttons, org switcher)
│   ├── Footer.tsx          Footer with links and social icons
│   ├── OrganizationSwitcher.tsx        Dropdown to switch active org; reads/writes active-org cookie
│   ├── CreateOrganizationModal.tsx     Modal form for org creation
│   ├── VolunteerOpportunitiesList.tsx  Paginated list (size=10), gated create button
│   └── CreateVolunteerOpportunityModal.tsx  Modal form for opportunity creation
├── layouts/
│   ├── AppLayout.tsx       Header + <Outlet /> + Footer
│   └── ProtectedRoute.tsx  Redirects to Keycloak if not authenticated
├── pages/
│   ├── HomePage.tsx                    Main page with VolunteerOpportunitiesList
│   ├── OrganizationSettingsPage.tsx    Org settings: general info + member management
│   ├── DatenschutzPage.tsx             Privacy policy (static)
│   └── ImpressumPage.tsx               Legal notice (static)
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

- `auth.user?.profile` — decoded id_token claims (sub, email, name, preferred_username, roles)
- `auth.user?.access_token` — Bearer token for API calls
- `active-org` cookie — active organization id (set by OrganizationSwitcher)
- Roles: `auth.user?.profile?.roles` — flat string array from Keycloak custom mapper

## API Client

`src/client/api-client.ts` is auto-generated from `backend/src/Api/wwwroot/openapi-v1.json` by NSwag on every backend build. Never edit it manually — changes will be overwritten.

Use `useApiClient()` hook in all components:
```ts
const api = useApiClient()
await api.getVolunteerOpportunities(page, 10)
await api.createOrganization({ name })
```

For one-off calls outside React (e.g., scripts), use `createApiClient(token)` directly.

## Environment Variables

Defined in `.env.development`. Exposed client-side via Vite (must use `VITE_` prefix).

| Variable | Dev value |
|---|---|
| `VITE_KEYCLOAK_AUTHORITY_URL` | `http://localhost:8080/realms/einsatzbereit` |
| `VITE_KEYCLOAK_CLIENT_ID` | `frontend` |
| `VITE_API_URL` | `http://localhost:5000` |

Accessed via `import.meta.env.VITE_*`.

## Role Checks

Roles come from `auth.user?.profile?.roles` (flat string array, custom Keycloak mapper).

```tsx
const roles = (Array.isArray(auth.user?.profile?.roles) ? auth.user!.profile.roles : []) as string[]
const isOrganisator = roles.includes('organisator')
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

Current protected routes:
- `/organizations/:organizationId/settings` → `OrganizationSettingsPage` (requires `organisator`)

**Note:** New API methods become available in `useApiClient()` only after running `dotnet build` in `backend/` (NSwag regenerates `src/client/api-client.ts`). During development, new page code may use `(api as any)` until the client is regenerated.

## Scripts

```bash
pnpm dev      # dev server on :4321
pnpm build    # build to dist/ (static files)
pnpm preview  # preview production build
pnpm check    # tsc --noEmit
pnpm lint     # eslint, zero warnings allowed
```

## Key Dependencies

| Package | Purpose |
|---|---|
| `vite` | Build tool + dev server |
| `react` 19 | UI framework |
| `react-router` v7 | Client-side routing |
| `react-oidc-context` | Keycloak OIDC (wraps oidc-client-ts) |
| `oidc-client-ts` | PKCE flow, token management |
| `@tailwindcss/vite` | Tailwind CSS 4 via Vite |

## Production

Static files in `dist/` served by nginx. `nginx.conf` handles SPA routing via `try_files $uri /index.html`.

**Important:** CORS must be configured on the backend to allow the frontend origin, since API calls are now cross-origin (no server-side proxy).
