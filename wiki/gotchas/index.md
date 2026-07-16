# Gotchas

Lessons learned while fixing real bugs - non-obvious traps worth avoiding next time.

- [Out-of-order async fetch responses](out-of-order-fetch-responses.md) - a slower earlier response can overwrite state a later one already set correctly
- [Inner-joining a hard-deleted row](inner-join-hard-deleted-row.md) - silently drops the other side's history instead of showing it as cancelled
- [NSwag client never hand-edited](nswag-client-never-hand-edit.md) - three generated files regenerate on backend build and get overwritten
- [Sandbox has no Aspire/Docker](sandbox-no-aspire-docker.md) - docker info succeeding doesn't mean DCP orchestration works
- [Keycloak test passwords must stay pre-hashed](keycloak-test-user-passwords-prehashed.md) - the realm's own password policy rejects them as plaintext
