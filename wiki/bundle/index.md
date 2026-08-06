---
okf_version: "0.1"
---

# Knowledge index

Root index of this OKF bundle (`wiki/bundle/`). Informal knowledge about Einsatzbereit -
traps, decisions, process, and reference - that complements the formal `docs/` and the
`CLAUDE.md`/`AGENTS.md` convention files. 21 concept pages, grouped below; each
section has its own index. This file is a routing table, not meant to be read cover to
cover. See `../AGENTS.md` for when to split a section further.

## Sections

- [Project](project/index.md) (2) - What the project is for and how the 1.0 launch is planned.
- [Process](process/index.md) (5) - Workflows and procedures - releasing, the mandatory deploy-and-verify flow, live Playwright scripts, EF migrations, and keeping this wiki self-building.
- [Gotchas](gotchas/index.md) (5) - Traps and non-obvious constraints, each learned from fixing a real bug or hitting a real wall.
- [Reference](reference/index.md) (4) - Stable reference material - the conventions the architecture tests enforce, the frontend stack and its lint gaps, the Keycloak realm, and a pointer to the formal ADRs/TDRs.
- [Decisions](decisions/index.md) (4) - Why the repo's tooling, autonomous routines, and testing boundaries are set up the way they are.
- [CI](ci/index.md) (1) - Recurring CI failure modes and their causes.
