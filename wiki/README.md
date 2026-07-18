# Project wiki

Informal knowledge about Einsatzbereit that doesn't fit elsewhere: gotchas
learned while fixing bugs, why-X-over-Y detail too small for a full ADR,
recurring CI failure causes, notes from `persona-simulation` runs. It follows
a self-building "LLM wiki" pattern - sources are distilled into short,
cross-linked concept pages rather than piled up raw. On disk it's an OKF (Open
Knowledge Format) v0.1 bundle, a lightweight in-house storage convention
rather than an external standard.

## What this complements, not replaces

- `docs/` - formal, reviewed arc42 architecture documentation, ADRs, TDRs.
  AsciiDoc, built to GitHub Pages. Authoritative for anything that belongs
  there.
- The per-directory `CLAUDE.md` files (root, `backend/`, `frontend/`,
  `keycloak/`, `docs/`, `.github/`) - stable conventions an agent needs on
  every session.
- This wiki - informal, fast-moving, agent-maintained. Markdown, lower
  ceremony than the arc42 docs.

## Input channels

Three kinds of source feed the wiki, all cited the same way once distilled,
none ranking above the others:

1. **`notes/`** - loose notes and ideas about Einsatzbereit, dropped in by
   hand as plain Markdown (see `notes/README.md`). Raw and read-only once
   added.
2. **The repo** - commits, code, hook scripts, `CLAUDE.md` files. A gotcha or
   decision already in the codebase is cited directly by repo-relative path,
   with no copy into `notes/`.
3. **GitHub issues and PRs** - cited directly by `#NNN` or URL.

See `AGENTS.md` for the full ingest/query/lint workflow, or the skills
directly: `/ingest`, `/query`, `/lint` (`.claude/skills/` at the repo root,
available in every session).

## Structure

```
wiki/
├── AGENTS.md          Ingest/query/lint workflow, vendor-neutral.
├── CLAUDE.md          `@AGENTS.md` - one-line import so Claude Code loads it.
├── TEMPLATE.md        Copy into bundle/ to start a new concept by hand.
├── WRITING_STYLE.md   Prose rule for every write to this bundle.
├── requirements.txt   `pyyaml`, for scripts/validate.py.
├── scripts/
│   └── validate.py    Conformance checker for the bundle.
├── notes/             Loose hand-dropped input notes (one of three channels).
└── bundle/            The OKF bundle - the only part validate.py scans.
    ├── index.md       Bundle root index.
    ├── log.md         Chronological, newest-first record of changes.
    └── ...            Concept pages (and sub-directories) live here.
```

Everything at the wiki root, plus `notes/`, is scaffolding or raw input. The
bundle is the `wiki/bundle/` subfolder: every `.md` under it other than
`index.md` and `log.md` (at any level) is a concept document.

## Status

Prototype scaffold: no concept pages yet, no CI wiring, no hook into
`issue-triage` or `persona-simulation`. Run `/ingest` against whatever lands
in `notes/` to try it, then decide whether it's worth wiring up further.

## Validating

```bash
pip install -r wiki/requirements.txt
python wiki/scripts/validate.py
```
