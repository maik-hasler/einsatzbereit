# Log

Chronological, newest-first record of changes to this bundle. Append one
entry per ingest/edit (see `AGENTS.md`), bold action prefix, ISO 8601 date.

- 2026-07-17 **Superseded** - the first repo-wide ingest (18 pages, see the
  entry below) was cleared entirely: a repo-wide check found essentially
  zero cross-linking between concepts (one reciprocal pair out of 18 pages,
  despite Ingest step 5 already saying to cross-link). Root cause: that
  batch was mined in one pass with no step forcing a check against what
  else already existed, so nothing pointed anywhere except its own source.
  Fix, not just a re-ingest: `tags` promoted to a required frontmatter
  field, every concept page now requires a `# Related` section
  (`scripts/validate.py`-enforced), Ingest step 5 rewritten from a
  one-line suggestion into a mandatory grep-and-bidirectional-link
  procedure, and Lint gained a relatedness audit with the same
  count-and-report rigor as the existing source-coverage audit. Bundle is
  back to zero concept pages - rebuilding through the new process, not
  hand-patched onto the old one.
- 2026-07-17 **Moved** - `ingest`/`query`/`lint` skills relocated from
  `wiki/.claude/skills/` to the repo-root `.claude/skills/`. No longer
  wiki-scoped or lazy-loaded (invoke as `/ingest`, `/query`, `/lint`, not
  `/wiki:ingest` etc.) - available in every Claude Code session now, same as
  `self-review`/`issue-triage`/`persona-simulation`.
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
