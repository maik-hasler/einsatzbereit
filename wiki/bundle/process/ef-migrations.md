---
type: "process"
title: "EF Core migration workflow and startup application"
description: "How to add a migration, where it applies automatically, and the snake_case singular-table convention that trips raw SQL."
tags:
  - ef-core
  - editorconfig
  - clean-architecture
  - data-integrity
timestamp: 2026-07-18
---

# Adding a migration

```bash
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api
```

`Infrastructure` is the migrations project (files land in `Infrastructure/Persistence/Migrations/`); `Api` is the startup project. At design time EF cannot build the real `ApplicationDbContext`, so `ApplicationDbContextFactory` (an `IDesignTimeDbContextFactory`) supplies one. That factory must mirror the runtime options - `UseNpgsql(...MigrationsAssembly...)` and `UseSnakeCaseNamingConvention()`. If a scaffolded migration ever looks wrong (missing tables, wrong column casing), the factory drifting from `Program.cs` is the first place to look.

# Where migrations get applied

Startup behaviour splits by environment in `Program.cs`:

- **Development**: migrations run automatically. The initializer calls `MigrateAsync`, then `SeedAsync`, then `BackfillOrganizationMembershipsAsync`.
- **Everywhere else** (staging, prod): application is opt-in, gated on the `Database:MigrateOnStartup` config flag. When set, it runs `MigrateAsync` and `BackfillOrganizationMembershipsAsync` - no seeding. When the flag is off, nothing migrates on boot and you must run `dotnet ef database update` yourself.

The practical trap: a new migration will not reach a staging or prod database just because it deploys. Either `Database:MigrateOnStartup` is enabled for that environment or the schema stays stale until someone applies it by hand. The `OrganizationMembership` backfill rides this same path, so whether membership data exists after a deploy depends on the same flag.

# The snake_case singular-table convention

`UseSnakeCaseNamingConvention()` maps entity names to **singular** snake_case tables, not plural: `VolunteerOpportunity` becomes `volunteer_opportunity`. EF-generated queries honour this automatically, so it is invisible until you hand-write SQL. A raw-SQL test helper assumed the plural `volunteer_opportunities` and CI's IntegrationTests failed with Postgres `42P01` ("relation ... does not exist") in #704. Any `FromSql`, direct `NpgsqlCommand`, or migration written by hand must use the singular table name.

# Auditing is automatic

`AuditableEntityInterceptor` populates `created_on` and `modified_on`. Do not set these fields in handlers or SQL.

# Scaffolded files fail editorconfig

`dotnet ef migrations add` writes files with spaces and a UTF-8 BOM. The repo default is tab indentation and no BOM, so a freshly scaffolded migration fails CI's editorconfig job unless you fix indentation and strip the BOM before committing - see ci-traps.

# Related

- [ci-traps](/ci/ci-traps.md) - scaffolded migration files fail the tabs/BOM editorconfig gate
- [opportunity-deletion-cascade](/gotchas/opportunity-deletion-cascade.md) - the singular snake_case naming bites the same read/raw-SQL code
- [auth-fresh-deploy-traps](/gotchas/auth-fresh-deploy-traps.md) - the OrganizationMembership backfill rides the migrate-on-startup path
- [backend-conventions](/reference/backend-conventions.md) - persistence sits inside the Infrastructure -> Domain layer rule
- [claude-check-setup](/decisions/claude-check-setup.md) - the ef-migration-check agent flags a missing migration for entity changes

# Citations

- backend/AGENTS.md:164-170
- backend/src/Api/Program.cs:138-158
- #704
