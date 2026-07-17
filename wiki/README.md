# Project wiki

Informal knowledge about Einsatzbereit that doesn't fit anywhere else: gotchas
learned while fixing bugs, why-we-picked-X-over-Y detail that doesn't warrant a
full ADR, recurring CI failure causes, notes from `persona-simulation` runs.
Proposed in [issue #701](https://github.com/maik-hasler/einsatzbereit/issues/701),
following the same self-building "LLM wiki" pattern (Andrej Karpathy's
[gist](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f), the OKF
v0.1 spec) already used in the author's personal wiki.

## What this complements, not replaces

- `docs/` - formal, reviewed arc42 architecture documentation, ADRs, TDRs
  (and, since this move, `docs/notes/` for this wiki's own raw input notes -
  see "Where things come from" below). AsciiDoc for the formal parts, built
  to GitHub Pages. Stays authoritative for anything that belongs there.
- The per-directory `CLAUDE.md` files (root, `backend/`, `frontend/`,
  `keycloak/`, `docs/`, `.github/`) - stable conventions an agent needs on
  every session.
- This wiki - informal, fast-moving, agent-maintained. Markdown, not
  AsciiDoc: lower ceremony than arc42, matching the personal wiki this is
  based on rather than the `docs/` format.

## Where things come from

Three input channels feed this wiki, not just one:

1. **`docs/notes/`** - your own loose notes and ideas about Einsatzbereit,
   dropped by hand as plain Markdown (see `docs/notes/README.md`). Raw and
   immutable once added, same convention as the personal wiki's `sources/`.
2. **The repo itself** - commits, code, hook scripts, existing `CLAUDE.md`
   files. A gotcha or decision already baked into the codebase gets cited
   directly by repo-relative path, no copy into `docs/notes/` needed.
3. **GitHub issues and PRs** - cited directly by `#NNN` or URL.

See `AGENTS.md` for the full ingest/query/lint workflow, or use the
directory-scoped skills directly: `/wiki:ingest`, `/wiki:query`, `/wiki:lint`.

## Structure

```
wiki/
├── AGENTS.md          Ingest/query/lint workflow, vendor-neutral.
├── CLAUDE.md          `@AGENTS.md` - one-line import so Claude Code loads it.
├── TEMPLATE.md        Copy into wiki/ to start a new concept by hand.
├── WRITING_STYLE.md   Prose rule for every write to this bundle.
├── index.md           Bundle root index.
├── log.md             Chronological, newest-first record of changes.
├── requirements.txt   `pyyaml`, for scripts/validate.py.
├── scripts/
│   └── validate.py    Conformance checker for this bundle.
└── .claude/
    └── skills/
        ├── ingest/
        ├── query/
        └── lint/
```

Every `.md` file directly under `wiki/` other than the files listed above is
a concept document; every `.md` file under a subdirectory that isn't
`scripts/` or `.claude/` is a concept document too.

## Status

Prototype scaffold only - no concept pages yet, no CI wiring, no hook into
`issue-triage` or `persona-simulation`. Try it with `/wiki:ingest` against
whatever you drop in `docs/notes/`, then decide via issue #701 whether it's
worth wiring up further.

## Validating

```bash
pip install -r wiki/requirements.txt
python wiki/scripts/validate.py
```
