# Process

Workflows and procedures - releasing, the mandatory deploy-and-verify flow, live Playwright scripts, EF migrations, and keeping this wiki self-building.

- [Deploy-and-verify is mandatory for every fix](deploy-verify-flow.md) - A fix is not done until it has been observed working on live staging, following a fixed ordered flow.
- [EF Core migration workflow and startup application](ef-migrations.md) - How to add a migration, where it applies automatically, and the snake_case singular-table convention that trips raw SQL.
- [Keeping the project wiki self-building](wiki-maintenance.md) - Why validate.py is not evidence of completeness, how notes/ are append-only, and where the bundle came from.
- [Tag-driven releases via a release/* branch](release-workflow.md) - How releases actually fire: push a release/vX.Y.Z branch (not a tag), which release-rc.yml promotes to a tag; only RC tags deploy to staging.
- [Writing live-staging Playwright scripts](live-playwright-scripts.md) - Write a throwaway script in a scratch directory, never scripts/, inlining the launch args and handling the two-step live login; a plain chromium.launch() dies under the sandbox egress proxy.
