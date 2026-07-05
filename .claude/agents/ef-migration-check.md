---
name: ef-migration-check
description: Checks whether an Entity Framework model change (new/changed entity, ApplicationDbContext, or a Fluent API configuration) has a matching new EF Core migration. Use proactively after editing anything under backend/src/Domain or backend/src/Infrastructure/Persistence.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

Compare the current diff (`git diff`) against
`backend/src/Infrastructure/Persistence/Migrations/` for a newly added
migration file pair (`<Timestamp>_<Name>.cs` + `.Designer.cs`) in the same
change.

Triggers to check for a matching migration:

- A new or changed aggregate/entity under `backend/src/Domain/**`
  (e.g. `Domain/Organizations/Organization.cs`).
- A new or changed Fluent API configuration under
  `backend/src/Infrastructure/Persistence/Configurations/`.
- A change to `ApplicationDbContext.cs` itself (new `DbSet<T>`, changed
  `UseSnakeCaseNamingConvention` setup, etc.).

If the model changed without a matching migration file in the diff, flag it
and give the exact command (run from `backend/`):

```
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api
```

Also flag it if the migration exists but its `Designer.cs` counterpart is
missing, or if the change looks like it should update
`AuditableEntityInterceptor` behaviour (new auditable field) without doing so.
Never create the migration yourself - report only.
