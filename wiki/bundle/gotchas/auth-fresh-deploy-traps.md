---
type: "gotcha"
title: "Authorization traps that only fire on a fresh deploy or import"
description: "A new per-org auth table without a backfill locks out existing organizers, and plaintext realm passwords crash a fresh Keycloak import."
tags:
  - authorization
  - keycloak
  - deploy-verify
  - ef-core
timestamp: 2026-07-18
---

Two auth failures share a shape: both pass every test and look fine in dev, then break the first time code meets pre-existing data or a clean import. Neither is visible in the diff.

# The per-org organizer table locked out every existing organizer

The organizer role was scoped per-organization by a new `organization_membership` table. `OwnershipGuard.EnsureIsOrganizerAsync` calls `IApplicationDbContext.IsOrganizerAsync`, which reads only that table. There is no Keycloak fallback at request time (`Application/Common/Authorization/OwnershipGuard.cs`, a shared Application helper, not under the Organizations feature folder). If a user has no row for the org, they get a 403 `Organization.NotOrganizer` on every org-modifying endpoint.

The migration `20260715212802_AddOrganizationMembership` creates the table and its unique index and nothing else. Its `Up` inserts zero rows. On a fresh deploy against production data that predates the table, every pre-existing organizer has zero membership rows, so every one of them is 403'd out of their own org. Olaf reproduced this live (#702).

Why nothing caught it: `SeedAsync` inserts the organizer membership row for the seed orgs (`ApplicationDbContextInitializer.cs`, `SeedOrg1Async`/`SeedOrg2Async`), so a clean local stack and fresh-seed integration tests both look correct. The lockout only exists in the interaction between the migration and the auth check running against rows that already existed before the table did. A diff review sees a table, an index, and a guard that all look internally consistent.

The fix is a one-time startup backfill, not a data migration. `BackfillOrganizationMembershipsAsync` finds every organization with no membership rows, pulls its organizer members from Keycloak (`IKeycloakOrganizationService.GetMembersAsync`, filtered to `IsOrganisator`), and inserts a row per organizer. It is idempotent (it skips orgs that already have rows) and runs after `MigrateAsync` on both startup paths in `Program.cs`: the `IsDevelopment` path (migrate, seed, backfill) and the staging `Database:MigrateOnStartup` path (migrate, backfill). Wiring it into only one path would leave staging or dev broken. Keycloak stays the source of truth for who is an organizer; the table is a local projection that the backfill seeds once from Keycloak, then the request-time guard reads without ever calling Keycloak again.

The general lesson: when a new table becomes the sole authority for an access decision, a `CreateTable` migration is only half the change. The other half is populating it from wherever the truth lived before, on the same startup path that runs the migration.

# Plaintext realm passwords crash a fresh Keycloak import

The test-user passwords in `einsatzbereit-realm.json` (`vera/vera123`, `olaf/olaf123`, `admin/admin123`) are stored **pre-hashed** as PBKDF2-SHA256, not as plaintext `value` fields. This is deliberate, and it is not about secrecy.

The realm's `passwordPolicy` is `length(8) and digits(1) and lowerCase(1) and upperCase(1)` (four rules). All three dev passwords lack an uppercase letter, so every one of them violates `upperCase(1)`; the two seven-character passwords (`vera123`, `olaf123`) also fail `length(8)`, while `admin123` at eight characters does not. Keycloak validates plaintext credentials against the password policy during `--import-realm`, so any plaintext `value` fails the import with `invalidPasswordMinUpperCaseCharsMessage` and Keycloak never starts. A running dev instance never re-validates, so this only bites a fresh import: CI, a clean local stack, or a first-time deploy. Pre-hashed credentials skip policy validation entirely, which is why they survive the import.

Do not "simplify" a pre-hashed credential back to a plaintext `value` to make it readable. To rotate a realm password: import the realm, set the new password in the Keycloak admin UI, then partial-export the user and copy the hashed credential back into the JSON. Never hand-write plaintext (`keycloak/AGENTS.md:58`).

# Related

- [keycloak-realm-config](/reference/keycloak-realm-config.md) - the realm JSON and its import behavior are where the password trap lives
- [ef-migrations](/process/ef-migrations.md) - the startup-backfill fix rides the migrate-on-startup path

# Citations

- #702 - Olaf reproduced the organizer lockout live on a fresh deploy against pre-existing data
- keycloak/AGENTS.md:58 - pre-hashed realm passwords, the `passwordPolicy`, the `invalidPasswordMinUpperCaseCharsMessage` import crash, and the rotate-via-UI-then-export procedure
