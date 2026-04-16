# Frontend — Astro 6 + React 19 + Tailwind CSS 4

## Architecture

Astro SSR (Node adapter, standalone mode). React used as islands for interactive components only.

```
src/
├── client/
│   ├── api-client.ts       NSwag-generated TypeScript client — DO NOT HAND-EDIT
│   └── api-instance.ts     Creates authenticated client with Bearer token from session cookie
├── components/
│   ├── *.tsx               React islands (interactive, use client:load in pages)
│   └── *.astro             Static components (Header, Footer)
├── layouts/
│   ├── BaseLayout.astro    Wraps all pages: Header + Footer + global styles
│   └── ProtectedPage.astro Extends BaseLayout; redirects to /api/login if no session
├── pages/
│   ├── api/                Server-side API routes (thin proxy to backend)
│   │   ├── login.ts        OIDC authorization code + PKCE initiation
│   │   ├── callback.ts     OIDC callback: code → token exchange, cookie write
│   │   ├── logout.ts       Clear session cookie + OIDC end_session
│   │   ├── organizations.ts        GET + POST proxy
│   │   └── volunteer-opportunities.ts  GET + POST proxy
│   ├── index.astro         Homepage (public)
│   ├── datenschutz.astro   Privacy policy
│   └── impressum.astro     Legal notice
├── styles/global.css       Tailwind directives
├── middleware.ts            Runs every request: parses session cookie → Astro.locals
└── env.d.ts                App.Locals types: user (JWT payload), activeOrgId
```

## Auth Flow

```
Browser → /api/login → Keycloak (authorization code + PKCE)
Keycloak → /api/callback → exchange code for tokens → store in httpOnly cookie
Every request → middleware.ts decodes id_token JWT → Astro.locals.user
```

- `Astro.locals.user` — JWT payload (sub, email, roles, etc.)
- `Astro.locals.activeOrgId` — from `active-org` cookie (set by OrganizationSwitcher)
- JWT is decoded without re-verification in middleware (already verified during OIDC exchange)
- Library: `openid-client` v6

## React Components

| Component | Purpose |
|---|---|
| `CreateOrganizationModal.tsx` | POST to `/api/organizations`, shows loading/error state |
| `CreateVolunteerOpportunityModal.tsx` | POST to `/api/volunteer-opportunities`, all opportunity fields |
| `OrganizationSwitcher.tsx` | Fetches orgs, sets `active-org` cookie, shows create-org button |
| `VolunteerOpportunitiesList.tsx` | Paginated list (size=10), gated create button for organisators |

Use components in Astro pages with `client:load`:
```astro
<VolunteerOpportunitiesList client:load canCreateOpportunity={isOrganisator} />
```

## API Client

`src/client/api-client.ts` is auto-generated from `backend/src/Api/wwwroot/openapi-v1.json` by NSwag on every backend build. Never edit it manually — changes will be overwritten.

`src/client/api-instance.ts` wraps it with the auth header:
```ts
// Pass access_token from Astro.locals or cookie — api-instance handles Bearer header
const client = createApiClient(accessToken);
await client.getVolunteerOpportunities(...);
```

## Environment Variables

Defined in `astro.config.ts` `env.schema` and `access: "secret"` (server-side only).  
Accessed via `import { VAR_NAME } from 'astro:env/server'`.

| Variable | Dev value |
|---|---|
| `KEYCLOAK_AUTHORITY_URL` | `http://localhost:8080/realms/einsatzbereit` |
| `REDIRECT_URI` | `http://localhost:4321/api/callback` |
| `API_URL` | `http://localhost:5000` |

Set in `.env.development`. Never expose in client-side code.

## Role Checks

Roles come from JWT via `Astro.locals.user.roles` (array of strings).

```astro
---
const isOrganisator = Astro.locals.user?.roles?.includes('organisator') ?? false;
---
```

Known roles: `user`, `organisator`, `admin`.

## Scripts

```bash
pnpm dev          # dev server on :4321
pnpm build        # build to dist/
pnpm preview      # preview production build
pnpm check        # type check (astro check)
pnpm lint         # eslint, zero warnings allowed
pnpm test:unit    # vitest (unit tests)
pnpm test:tests   # playwright (e2e)
```

## Testing

### Unit tests (Vitest)
- Config: `vitest.config.ts` — jsdom environment, globals enabled
- Location: `tests/*.test.tsx`
- Setup: `tests/setup.ts` (imports jest-dom matchers)
- Run: `pnpm test:unit`

### E2E tests (Playwright)
- Config: `playwright.config.ts` — Chromium only, base URL `:4321`
- Location: `tests/*.spec.ts`
- Webserver: auto-builds and runs `pnpm build && pnpm preview`
- Run: `pnpm test:tests`

## Adding a New Page

```astro
---
// src/pages/my-page.astro
import BaseLayout from '../layouts/BaseLayout.astro';
const { user } = Astro.locals;
---
<BaseLayout title="My Page">
  <!-- content -->
</BaseLayout>
```

For protected pages, swap `BaseLayout` for `ProtectedPage` — it redirects unauthenticated users.

## Adding a New API Route

```ts
// src/pages/api/my-resource.ts
import type { APIRoute } from 'astro';

export const GET: APIRoute = async ({ locals, cookies }) => {
  const { user } = locals;
  if (!user) return new Response(null, { status: 401 });
  // call backend via createApiClient(accessToken)
};
```

## Key Dependencies

| Package | Purpose |
|---|---|
| `astro` 6.1.7 | SSR framework |
| `@astrojs/react` | React integration |
| `@astrojs/node` | Node.js SSR adapter |
| `openid-client` 6.8.2 | OIDC/OAuth2 client |
| `@tailwindcss/vite` | Tailwind CSS 4 via Vite |
| `astro-icon` | SVG icon integration |
| `@iconify-json/simple-icons` | Icon set |
