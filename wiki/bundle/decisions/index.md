# Decisions

Why the repo's tooling, autonomous routines, and testing boundaries are set up the way they are.

- [The autonomous routines and their guardrails](autonomous-routines.md) - issue-triage, its persona-simulation fallback, and deep-lens-review - plus the boundaries the owner keeps for themselves.
- [The report-only self-review machinery in .claude](claude-check-setup.md) - Five check agents each scoped to a gap CI cannot catch, a self-review skill that fans out to them, and the hooks that enforce the rest.
- [The root scripts/ folder and root package.json were removed](scripts-folder-removed.md) - 106 tracked .mjs/.sh files and the Playwright pin they depended on are gone; live-verification scripts are now scratch-only and never committed.
- [Frontend component-level tests are not adopted](frontend-component-tests-not-adopted.md) - VisualTests covers component/page behavior instead; Vitest stays scoped to `src/lib/` pure functions.
