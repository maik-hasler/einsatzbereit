# Log

Chronological, newest-first record of changes to this bundle. Append one
entry per ingest/edit (see `AGENTS.md`), bold action prefix, ISO 8601 date.

- 2026-07-17 **Moved** - the loose-notes input channel relocated from
  `wiki/sources/` to `docs/notes/`, resolving issue #701's open "where do
  loose notes live" question in favor of `docs/`. Same channel, same
  ingest pipeline, new address - see `docs/notes/README.md`.
- 2026-07-16 **Added** - first repo-wide ingest: 18 concept pages across
  `gotchas/` (5), `decisions/` (7), `ci/` (5), `persona-notes/` (1). Mined
  from git commit history, `CLAUDE.md` files, ADRs, GitHub issues/PRs, and
  CI config - see each page's Citations section for the exact source.
  `sources/` itself is still empty; nothing dropped there yet.
- 2026-07-16 **Added** - scaffolded the wiki bundle (empty: no concept pages
  yet). See [issue #701](https://github.com/maik-hasler/einsatzbereit/issues/701).
