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

E2E tests live in the backend under `backend/tests/VisualTests/` (TUnit.Playwright + Aspire). Run from the backend:

```bash
cd ../backend
dotnet test tests/VisualTests
```

The Aspire AppHost provisions the full stack — no need to start the frontend dev server separately.
