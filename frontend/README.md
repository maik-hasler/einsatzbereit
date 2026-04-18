# Frontend

Vite SPA — React 19, React Router v7, Tailwind CSS 4, react-oidc-context (Keycloak PKCE).

## Commands

Run from `frontend/`:

| Command        | Action                                      |
| :------------- | :------------------------------------------ |
| `pnpm install` | Install dependencies                        |
| `pnpm dev`     | Dev server at `http://localhost:4321`       |
| `pnpm build`   | Production build to `./dist/`               |
| `pnpm preview` | Preview production build locally            |
| `pnpm check`   | TypeScript type check (`tsc --noEmit`)      |
| `pnpm lint`    | ESLint — zero warnings allowed              |

## E2E Tests

```bash
pnpm exec playwright test
```

Requires all services running (`docker compose up -d` from repo root). Global setup starts Docker automatically if not already running.

See [`tests/`](tests/) for spec files and helpers.
