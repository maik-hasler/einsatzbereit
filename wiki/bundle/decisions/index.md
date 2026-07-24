# Decisions

Why the repo's tooling and autonomous routines are set up the way they are.

- [The autonomous routines and their guardrails](autonomous-routines.md) - issue-triage, its persona-simulation fallback, and deep-lens-review - plus the boundaries the owner keeps for themselves.
- [The report-only self-review machinery in .claude](claude-check-setup.md) - Five check agents each scoped to a gap CI cannot catch, a self-review skill that fans out to them, and the hooks that enforce the rest.
- [The root scripts/ folder and root package.json were removed](scripts-folder-removed.md) - 106 tracked .mjs/.sh files and the Playwright pin they depended on are gone; live-verification scripts are now scratch-only and never committed.
- [Bookmark-compat redirects get a 6-month retention window, not indefinite life](bookmark-compat-redirect-retention.md) - the three unlinked redirect-only routes in App.tsx (/account, /achievements, /opportunities) each get an explicit remove-after date instead of living forever unexamined.
