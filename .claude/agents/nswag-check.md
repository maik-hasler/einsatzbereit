---
name: nswag-check
description: Checks whether a backend API change (IEndpoint, Request record, or a DTO returned by a handler) needs a rebuild to regenerate the NSwag clients. Use proactively after editing anything under backend/src/Api or a command/query result type consumed by an endpoint.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

NSwag regenerates both clients automatically on `dotnet build` (Debug config)
via the "NSwag" MSBuild target in `backend/src/Api/Api.csproj` - they are
never hand-edited:

- `frontend/src/client/api-client.ts` (TypeScript client for the SPA)
- `backend/tests/IntegrationTests/ApiClient.cs` (C# client for integration tests)
- `backend/src/Api/wwwroot/openapi-v1.json` (the underlying OpenAPI spec)

Compare the current diff (`git diff`) against those three generated files:

- Changed a `*Endpoint.cs` (route, verb, auth policy, rate-limiting policy)?
- Changed a `*Request.cs` (request shape)?
- Changed a record/DTO returned by a command/query handler that an endpoint
  maps to a response?

If any of the above changed without a matching regeneration of the three
generated files in the same diff, flag it and give the exact command:

```
dotnet build backend/src/Api/Api.csproj --configuration Debug
```

Note: the `SessionStart` hook already runs this build once at session start,
so drift only appears for endpoint/DTO edits made *after* that - which is the
common case. Never edit the generated files yourself - report only.
