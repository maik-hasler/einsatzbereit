# Decisions

Why-X-over-Y calls that don't warrant a full ADR in `docs/ADRs/`.

- [Why the Einsatzbereit wiki exists](why-wiki-exists.md) - issue #701's proposal and its still-open questions
- [OwnershipGuard on org-scoped handlers](ownership-guard-org-scoped-handlers.md) - a role check alone doesn't prove membership in the target org
- [Order before projecting in EF Core](order-before-project-ef-core.md) - OrderBy must run on the entity property, not the projected DTO
- [Release RCs are branches, not tags](release-rc-branch-not-tag.md) - the sandbox's git proxy can't push tags directly
- [Keycloak Register via a second UserManager](keycloak-register-separate-usermanager.md) - one metadata override instead of a second auth flow
- [Reviving the stale ddd-improvements branch](stale-ddd-improvements-branch.md) - why a 39-commit-stale branch isn't a simple merge
- [needs-decision blocks autonomous implementation](needs-decision-label-convention.md) - a live example of the label doing its job
