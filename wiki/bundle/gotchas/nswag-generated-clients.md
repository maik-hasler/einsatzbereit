---
type: "gotcha"
title: "NSwag-generated clients are hook-protected, never hand-edit"
description: "Three generated files regenerate from the backend build and are blocked by a PreToolUse hook; change the API shape at the source instead."
tags:
  - nswag
  - generated-code
  - hooks
  - api-client
timestamp: 2026-07-18
---

# The trap

Three files look like ordinary source but are NSwag output. Editing one to fix a wrong type, add a method, or tweak a DTO is the intuitive move and the wrong one - the next backend build overwrites your change, and a `PreToolUse` hook blocks the Edit/Write before it even lands.

The three files:

- `frontend/src/client/api-client.ts` - the typed TS client the frontend calls
- `backend/tests/IntegrationTests/ApiClient.cs` - the C# client used by integration tests
- `backend/src/Api/wwwroot/openapi-v1.json` - the OpenAPI document both clients derive from

# How they regenerate

`dotnet build backend/src/Api/Api.csproj --configuration Debug` rebuilds all three. `OpenApiGenerateDocumentsOnBuild` emits the OpenAPI doc, a `RenameOpenApiDocuments` target renames it to `openapi-v1.json`, then the `NSwag` MSBuild target (runs `AfterTargets="PostBuildEvent"`, `Condition` is Debug only) generates the two clients from it. Release builds skip the NSwag target, so a Release build will not refresh the clients.

The `SessionStart` hook (`.claude/scripts/session-start.sh`) runs this Debug build up front, so the three files are already current when your session begins. Drift only appears once you edit an endpoint or DTO yourself. When you do, rebuild before trusting the client.

# The protect hook

`.claude/hooks/protect-generated-clients.sh` is wired as a `PreToolUse` hook on `Edit|Write`. It reads the target `file_path`, normalizes backslashes to forward slashes with `tr '\\' '/'` (so Windows-style paths still match), and suffix-matches the three files. On a match it prints the reason to stderr and `exit 2`, which blocks the edit. There is no override flag - the hook is the enforcement, not a warning.

# Do this instead

To change the API shape, edit the real source and rebuild:

1. Change the endpoint, its `Request` record, or the DTO/response record.
2. Run `dotnet build backend/src/Api/Api.csproj --configuration Debug`.
3. The client and OpenAPI doc regenerate; the frontend consumes the new method through `useApiClient()`.

During frontend-first work the new client method does not exist yet, so page code may call `(api as any).newMethod(...)` until the backend build regenerates `api-client.ts`. Treat that cast as a temporary marker, not a shipped state - rebuild and drop it before the diff is reviewed.

# Related

- [backend-conventions](/reference/backend-conventions.md) - the API shape lives in endpoints/records/DTOs that regenerate the client
- [frontend-conventions](/reference/frontend-conventions.md) - the frontend consumes api-client.ts via useApiClient() and cannot patch it directly
- [claude-check-setup](/decisions/claude-check-setup.md) - the nswag-check agent and the protect hook both guard these files

# Citations

- AGENTS.md:24
- backend/AGENTS.md:78-80
- .claude/hooks/protect-generated-clients.sh
- frontend/AGENTS.md:52-64
