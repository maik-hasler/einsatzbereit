---
type: "decision-note"
title: "The report-only self-review machinery in .claude"
description: "Five check agents each scoped to a gap CI cannot catch, a self-review skill that fans out to them, and the hooks that enforce the rest."
tags:
  - self-review
  - autonomous
  - nswag
  - a11y
  - i18n
  - hooks
timestamp: 2026-07-18
---

# What this is

`.claude/` holds the pre-PR safety layer for an unsupervised routine that opens PRs with no human review before them. It has three parts: five report-only check agents, a `/self-review` skill that dispatches to them, and hooks that enforce what an agent might forget. Everything here reports; nothing here fixes on its own initiative.

# The agents are scoped to the negative space of CI

Each agent in `.claude/agents/` is defined by what CI, ESLint, or the compiler already guarantees, so it does not waste effort re-checking that. All five set `disallowedTools: Write, Edit` and their `tools` are read-only (`Bash, Read, Grep, Glob`). They flag and stop.

- **nswag-check**: fires on `backend/src/Api` endpoint/`Request`/DTO edits. The three generated files (`frontend/src/client/api-client.ts`, `backend/tests/IntegrationTests/ApiClient.cs`, `backend/src/Api/wwwroot/openapi-v1.json`) regenerate on `dotnet build --configuration Debug` via the NSwag MSBuild target. Drift is only possible for edits made *after* SessionStart already ran that build once, which is the common case.
- **ef-migration-check**: fires on `backend/src/Domain` or `backend/src/Infrastructure/Persistence` edits. Checks for a matching new migration pair (`<Timestamp>_<Name>.cs` + `.Designer.cs`). Also flags a missing `Designer.cs`, and an entity change that should touch `AuditableEntityInterceptor` but does not.
- **architecture-check**: mirrors `backend/tests/ArchitectureTests` (layer dependencies, `Endpoint`/`Command`/`Query` naming, no direct `IRequest<T>`, mandatory `.RequireRateLimiting` with a `RateLimitingPolicies.Read`/`.Write` policy). It catches these before the test suite has to, but the tests remain the source of truth.
- **a11y-check**: deliberately narrow. `eslint-plugin-jsx-a11y` already blocks missing `alt`, unlabelled controls, and bare `onClick`, so it skips those. It checks only the project patterns ESLint has no rule for (modal backdrop-button split, stretched-`Link` clickable cards, `aria-hidden`/`<title>` on SVGs, no `href="#"`) and whether a new route in `src/App.tsx` has a matching axe test in `AccessibilityTests.cs`. A new page with no test gets zero axe coverage silently, forever.
- **i18n-check**: the one gap nothing else covers. `en.json` and `de.json` share a nested key tree and no CI step compares them (`eslint-plugin-i18next` only runs `no-literal-string`). It diffs the two key sets and flags added/renamed/stale keys, structural drift, and a `t("...")` call that resolves in neither file.

# /self-review dispatches, it does not re-inspect

`.claude/skills/self-review/SKILL.md` is mandatory before opening a PR (root `AGENTS.md`, "Mandatory: Deploy and verify"). It runs a P1-P4 diff review (bugs and security first, then design and performance, then readability, then style) and stops when the code is clean rather than inventing issues. When the diff touches a domain the agents cover, it runs the matching agent and folds in its findings instead of shallowly re-checking: Api or a returned DTO -> nswag-check; Domain/Persistence -> ef-migration-check; new endpoint/handler or a type moved between layers -> architecture-check; a `.tsx` file -> a11y-check; locale files or a new `t()` call -> i18n-check. It skips generated files and EF migrations, and treats missing tests on new logic as P2.

# The hooks enforce what an agent could skip

**protect-generated-clients.sh** (PreToolUse on `Edit|Write`) hard-blocks any edit to the three NSwag files by exiting 2, and points the caller at the endpoint/Request/DTO plus the rebuild command. This is the enforcement behind nswag-check's advice; the agent recommends, the hook forbids.

**pre-stop-verify.sh** (Stop hook) runs `dotnet build` when `backend/src` changed and `pnpm lint` + `pnpm check` when `frontend/src` changed, so a broken build never ships to a PR. Two design decisions matter here:

- It **fails open**. There is no documented framework loop-prevention field for the Stop hook, so it self-imposes a cap of 2 blocks per session (a counter file keyed by `session_id`); after that it exits 0 and lets the turn end even if the build is still broken. Better to occasionally miss a break than to trap the routine in a loop.
- It **exports `PATH` itself** (`export PATH="$HOME/.dotnet:$PATH"`). SessionStart only appends that line to `~/.bashrc`, which a non-interactive hook shell never sources; without this line `dotnet` is "not found" and every backend change would falsely fail the build check.

# SessionStart front-loads the slow setup

`.claude/scripts/session-start.sh` does three things in order: installs SDK 10.0.300 via `dotnet-install.sh` only if `dotnet` is absent, then builds `Api.csproj` in Debug to regenerate the NSwag clients up front (so client drift can only appear for later edits), then runs `pnpm format:write` so no Prettier violations get committed. Plugins are declared in `.claude/settings.json`: the `dotnet/skills` marketplace (`dotnet-aspnetcore`, `dotnet-test`, `dotnet-nuget`, `dotnet-data`) plus the official `csharp-lsp`, `typescript-lsp`, and `playwright` plugins.

# Related

- [deploy-verify-flow](/process/deploy-verify-flow.md) - why: step 1 of the mandatory flow is /self-review
- [nswag-generated-clients](/gotchas/nswag-generated-clients.md) - why: nswag-check and the protect hook are the enforcement for these files
- [frontend-conventions](/reference/frontend-conventions.md) - why: a11y-check and i18n-check cover exactly the frontend gaps CI misses
- [ef-migrations](/process/ef-migrations.md) - why: ef-migration-check flags a missing migration for entity changes
- [autonomous-routines](/decisions/autonomous-routines.md) - why: these agents and skills are the tooling the unsupervised loops rely on

# Citations

- `.claude/agents/`
- `.claude/skills/self-review/SKILL.md`
- `.claude/hooks/protect-generated-clients.sh`
- `.claude/hooks/pre-stop-verify.sh`
- `.claude/scripts/session-start.sh`
- `.claude/settings.json`
