---
type: gotcha
title: Keycloak realm test-user passwords must stay pre-hashed, not plaintext
description: The realm's own password policy rejects the dev test passwords (vera123, olaf123, admin123) as plaintext during --import-realm, so they're stored pre-hashed instead.
tags: [keycloak, auth, dev-environment]
timestamp: 2026-07-16
---

# Schema

A realm import validates plaintext credential `value` fields against that same realm's `passwordPolicy` - a password policy strict enough for production can reject a realm's own seeded dev/test passwords if they're given as plaintext, crashing the import. Storing the credential pre-hashed skips that validation.

# Examples

`keycloak/realms/einsatzbereit-realm.json` stores `vera/vera123`, `olaf/olaf123`, and `admin/admin123` pre-hashed (PBKDF2-SHA256), not as plaintext `value` fields. The realm's `passwordPolicy` (`upperCase(1)`, `length(8)`) would reject these short dev passwords during `--import-realm` if they were given as plaintext, crashing a fresh import with `invalidPasswordMinUpperCaseCharsMessage` - meaning CI, a clean local stack, or a first-time deploy would never start.

To rotate one of these passwords: import the realm, set the new password via the Keycloak admin UI, then partial-export the user, rather than editing the `value` field by hand.

# Citations

- `keycloak/CLAUDE.md` (Test Users section)
- `keycloak/realms/einsatzbereit-realm.json`
