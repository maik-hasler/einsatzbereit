# Einsatzbereit

English-source UI strings and code. See `CONTRIBUTING.md`'s Language Convention for the full breakdown.

## Monorepo Structure

```
einsatzbereit/
├── backend/        .NET 10 Clean Architecture API        → backend/AGENTS.md
├── frontend/       Vite SPA + React 19 + Tailwind CSS 4  → frontend/AGENTS.md
├── keycloak/       Custom Keycloak image + realm config  → keycloak/AGENTS.md
├── docs/           arc42 architecture docs + ADRs        → docs/AGENTS.md
└── .github/        CI workflows + issue templates        → .github/AGENTS.md
```

## Tech Stack (quick ref)

See README.md's Tech Stack table for the full breakdown.

## Development Setup

Required: .NET SDK **10.0.400** (enforced via `backend/global.json`).

```bash
dotnet run --project backend/src/Aspire/AppHost
```

Aspire AppHost provisions Postgres, Keycloak, backend API, and the Vite frontend. URLs surface in the Aspire dashboard. See README.md's Services and Test users tables for the full list.

## Key Conventions

- Feature folders: `{Layer}/{Domain}/{Feature}/v1/` in the backend (`Api/`, `Application/` and `Domain/` all repeat the same module folders). The frontend is cut by artifact kind instead (`pages/`, `components/`, `hooks/`, `lib/`), with organizer routes grouped under `pages/app/` - see chapter 5 of the arc42 docs
- Routes: `/v{version:apiVersion}/...`, namespaces: `.v1`
- Commits, commands/queries/DTOs, and async conventions: see `CONTRIBUTING.md`'s Code Style and Commit Messages sections
- **Never use Unicode dashes** (U+2013 en dash, U+2014 em dash) in source files - write plain ASCII hyphens (`-`) instead; CI rejects non-ASCII dashes. The one exception is German user-facing content - `frontend/src/locales/de.json` and `backend/src/Infrastructure/Email/Templates/de.json` - which uses the en dash (Gedankenstrich) as German typography requires; see `CONTRIBUTING.md`'s Language Convention
- **Tab indentation is the default** (`.editorconfig`'s `[*]` rule) - shell scripts, AsciiDoc (`.adoc`), and PlantUML (`.puml`) all use tabs. Only `.md`, `.json`, `.yml`/`.yaml`, and `.py` (PEP 8) are overridden to spaces. CI's `editorconfig` job enforces this; when writing `.adoc` prose keep paragraphs on one unwrapped line rather than hand-wrapping with space-indented continuation lines

## Claude Code Configuration

`.claude/` holds the self-review setup for this repo (all report-only, never
edit on your own initiative):

- **Agents** (`.claude/agents/`) - proactive subagents invoked automatically
  when their trigger condition matches, no explicit request needed:
  `nswag-check` (endpoint/DTO changes vs. generated clients),
  `ef-migration-check` (entity changes vs. EF Core migrations),
  `architecture-check` (Clean Architecture layer/naming/rate-limiting rules),
  `a11y-check` (frontend components vs. the a11y conventions below - scoped
  to only what ESLint's `jsx-a11y` ruleset can't already catch, see the
  agent file for why), `i18n-check` (`en.json`/`de.json` translation key
  parity - nothing else in CI checks this).
- **Skills** - `.claude/skills/self-review/` (`/self-review`, its frontmatter
  description covers what it does; run it before opening a PR).
  `.claude/skills/lens/` is this repo's autonomous routine and on-demand
  review tool: one lens per run - bugs, dead code, dead features, repo
  hygiene, docs quality, test gaps, CI, security, contributor accessibility,
  accessibility, code/comment complexity, or comment bloat - chosen by
  triage or named by the user. Report-only: files GitHub issues (label
  `lens`, capped at 5/run), never code or a PR.
  `.claude/skills/frontend-design/` (vendored from `anthropics/skills`,
  Apache-2.0, `LICENSE` alongside it) pushes frontend redesign work
  toward a deliberate, non-generic visual direction - typography, color
  theming, motion, spatial composition - instead of generic AI-layout
  defaults; load it before visual/layout changes to frontend components
  or pages.
- **Hooks** - `.claude/hooks/protect-generated-clients.sh` blocks Edit/Write
  on the three NSwag-generated files (see "API client" row above).
  `.claude/hooks/pre-stop-verify.sh` (`Stop` hook) runs `dotnet build`/`pnpm lint`+`check`
  once before ending a turn if backend/frontend source changed, blocking
  only on an actual failure (capped at 2 blocks per session so it fails
  open rather than risk a loop) - a safety net since this routine has no
  human review before a PR goes out. The `SessionStart` hook installs the
  .NET SDK version pinned in `backend/global.json` automatically via
  `dotnet-install.sh` in Claude Code web/cloud sessions if `dotnet` is not
  already on `PATH` (see this file's Development Setup for the SDK
  requirement itself).
- **Plugins** - the `dotnet/skills` marketplace (`dotnet-aspnetcore`,
  `dotnet-test`, `dotnet-nuget`, `dotnet-data`) plus `csharp-lsp`,
  `typescript-lsp`, and `playwright` (live browser control) are enabled in
  `.claude/settings.json`. **MCP tool grants don't propagate to an
  `Agent`-tool subagent** - drive browser sessions (e.g. a design review)
  in the current session directly, never delegate them. Availability can
  also vary turn-to-turn even in the main session - `ToolSearch` for
  `browser_navigate` first.

## Sandbox Limitations (Claude Code on the web)

- **No reliable Docker** - `dotnet run --project backend/src/Aspire/AppHost`, the `IntegrationTests` project (Aspire), and the `VisualTests` project (Aspire + Playwright) all need real container networking. Don't try to run them locally in a web/cloud session, even if `docker info` succeeds - Aspire/DCP orchestration still fails. Verify locally with `dotnet build` + `Application.UnitTests` + `ArchitectureTests` (no Docker needed); CI's `dotnet.yml` runs the full suite including `IntegrationTests`/`VisualTests` on a real runner.
- **Direct pushes to `main` are blocked** by the git proxy (working-branch only) - always commit to the designated `claude/...` branch and open a PR, even if an instruction says to work "directly on main".
- **Run the `editorconfig` check the way CI does** - fetch the pinned release asset directly (`curl -sSL https://github.com/editorconfig-checker/editorconfig-checker/releases/download/v4.0.1/editorconfig-checker-linux-amd64.tar.gz | tar xz`, then run `./editorconfig-checker -config .editorconfig-checker.json`); keep the version in step with `EC_VERSION` in `.github/workflows/lint.yml`. Do not reach for `npx editorconfig-checker` - the wrapper resolves its binary through the GitHub releases API, which the sandbox's git proxy answers with 403, and it pins nothing. Worth running whenever a change adds a `.py`, `.adoc` or `.puml` file: nothing else in the local toolchain checks indentation, so the first signal is otherwise a red PR.
- **This repo ships fast** - `git fetch origin main` and skim recent commits before any review/analysis task, not just implementation work; assuming last week's state is current wastes most of a review's effort re-finding what already shipped.

## Releases (autonomous from Claude Code on the web)

Releases are driven by tags. The Claude Code on the web git proxy blocks tag pushes (working-branch only), so **do not** ask the user to `git push` a tag - push a `release/vX.Y.Z[-rc.N]` branch instead and let `.github/workflows/release-rc.yml` promote it. Full flow + the one-time `RELEASE_TOKEN` setup are documented in `.github/AGENTS.md` under "Cutting a release from Claude Code on the web". A release ends at published GHCR images and a GitHub Release - nothing in this repository runs or hosts the app.
