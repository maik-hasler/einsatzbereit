---
type: gotcha
title: The NSwag-generated API client is never hand-edited - it regenerates on backend build
description: frontend/src/client/api-client.ts, backend/tests/IntegrationTests/ApiClient.cs, and backend/src/Api/wwwroot/openapi-v1.json are generated from the backend's OpenAPI spec and get silently overwritten by any dotnet build.
tags: [backend, frontend, nswag, build-pipeline]
timestamp: 2026-07-16
---

# Schema

Three files in this repo are generated, not hand-written: `frontend/src/client/api-client.ts`, `backend/tests/IntegrationTests/ApiClient.cs`, and `backend/src/Api/wwwroot/openapi-v1.json`. NSwag regenerates all three from the backend's OpenAPI spec on every `dotnet build` of `backend/src/Api/Api.csproj` (Debug config), via an MSBuild target. A hand edit to one of them is silently overwritten on the next build - and, going the other way, editing an `IEndpoint` route/verb/policy, a `Request` record, or a DTO an endpoint returns without rebuilding leaves the checked-in generated clients out of sync with the actual backend, which CI and the frontend both rely on.

# Examples

Root `CLAUDE.md`'s Tech Stack table states the rule directly: "API client | NSwag-generated - never hand-edit `api-client.ts`". `backend/CLAUDE.md`'s Adding a Feature section repeats it for the backend side: OpenAPI regenerates automatically on `dotnet build`, and `api-client.ts` regenerates with it.

`.claude/hooks/protect-generated-clients.sh` enforces this at the tool level by blocking `Edit`/`Write` on all three files. `.claude/agents/nswag-check.md` is the proactive check that runs after an endpoint/DTO change and confirms the rebuild command `dotnet build backend/src/Api/Api.csproj --configuration Debug` was actually run, so the three files reflect the new shape.

Until a backend rebuild lands, `frontend/CLAUDE.md`'s Routing section notes that new page code may temporarily call `(api as any)` for a not-yet-generated method, as a stopgap rather than a hand edit.

# Citations

- `CLAUDE.md` (Tech Stack table, Claude Code Configuration section)
- `backend/CLAUDE.md` (Adding a Feature section)
- `frontend/CLAUDE.md` (API Client section, Routing section)
- `.claude/hooks/protect-generated-clients.sh`
- `.claude/agents/nswag-check.md`
