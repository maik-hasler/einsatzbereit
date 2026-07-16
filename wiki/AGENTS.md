# AGENTS.md

Informal knowledge about Einsatzbereit, stored as an OKF (Open Knowledge
Format) bundle - see `README.md` and
[issue #701](https://github.com/maik-hasler/einsatzbereit/issues/701) for why
this exists alongside `docs/` and the per-directory `CLAUDE.md` files. If you
are a coding agent working in this repo, you are the wiki's maintainer, not
just an editor. OKF standardizes the storage format only; the operations
below restore the self-building behavior of the "LLM wiki" pattern this
format is based on. Follow them whenever you touch this bundle.

See @WRITING_STYLE.md for the prose rule - applies to every write, not just
ingest.

If you're Claude Code, prefer the dedicated skills over re-reading this file
end to end: `/wiki:ingest`, `/wiki:query`, `/wiki:lint` (`.claude/skills/`
under `wiki/`). They're self-contained copies of the operations below, kept
lazy-loaded so they don't cost context on every session the way this file
does. Other agents should follow this file directly. If you change the
workflow, update both this file and the matching skill.

## Three input channels, one bundle

- **`sources/`** - your own loose notes and ideas about Einsatzbereit,
  dropped by hand as plain Markdown or text. Immutable once added: read from
  it, never edit or delete anything in it. Fully enumerable, so this is the
  one channel the coverage-audit rules below hold to strict K=0 accounting.
- **The repo itself** - commits, code, hook scripts, existing `CLAUDE.md`
  files. Cite directly by repo-relative path (optionally `@<commit-sha>`
  when the point is what a specific commit did). Not exhaustively
  enumerable the way `sources/` is, so treat coverage here as best-effort /
  on-request ("ingest recent gotchas from the last N commits", "ingest
  issue #x") rather than something a lint pass can claim complete.
- **GitHub issues and PRs** - cited directly by `#NNN` or full URL. Same
  best-effort coverage caveat as the repo channel.
- **`wiki/`** - the OKF bundle itself. Its root is `wiki/index.md` /
  `wiki/log.md`; everything else under it that isn't `sources/`, `scripts/`,
  or `.claude/` is a concept file or directory. You own this layer entirely.

Everything else at `wiki/`'s own root (`README.md`, `AGENTS.md`, `CLAUDE.md`,
`TEMPLATE.md`, `WRITING_STYLE.md`, `requirements.txt`, `scripts/`, `.claude/`)
is repo scaffolding - not part of the bundle, not scanned by
`scripts/validate.py`.

## Ingest - when given a new source, or asked what's missing

The wiki is a compiled, compounding artifact, not a static pile of documents
(this pattern is [Andrej Karpathy's LLM-wiki
idea](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)). A
false "already ingested" verdict silently breaks that compounding loop, so
treat completeness as something you prove every time for the `sources/`
channel specifically, never something you assume from a prior session, a
memory note, or a clean `validate.py` run - `validate.py` only checks
frontmatter shape on pages that already exist; it has no notion of
`sources/` and cannot detect missing ingestion.

Two modes for the `sources/` channel - pick one before doing anything else:

- **Targeted**: the request names one specific existing path under
  `sources/`. Run steps 1-8 below on it.
- **Coverage audit**: the request is bare ("ingest", "what's missing",
  "ingest everything") or names no specific path. Never skip straight to
  "everything's covered" here. Glob `sources/**/*` fresh (files and dirs,
  not just top-level entries), re-run `git status --short sources/` (path is
  `wiki/sources/` from the repo root) to catch anything added or staged
  mid-session, and grep each enumerated path against every wiki page's
  `# Citations` section. Report concrete numbers - "N found, M cited, K
  uncited: `<list>`" - a bare "looks complete" is not a valid conclusion.
  Then run Targeted mode on each uncited item in turn.

For the repo and issue/PR channels, ingest on request rather than trying to
audit them exhaustively - e.g. "ingest the gotcha behind commit `<sha>`" or
"ingest what issue #x settled." Say so explicitly if asked for a full
coverage claim across these two channels; it isn't one this bundle can make.

1. Read the source in full before writing anything (the raw file under
   `sources/`, the commit/diff, or the issue/PR thread). For a long source
   that doesn't fit in one read, split it into smaller pieces at whatever
   natural structure it has and ingest piece by piece, updating the same
   wiki pages incrementally rather than holding the whole thing in context.
2. Decide, per concept touched:
   - **New page** only if it's a distinct entity/concept other pages would
     link to. Check `type` values already used in the bundle before
     inventing one - reuse an existing type rather than creating a
     near-duplicate. Starter types for this bundle: `gotcha` (a lesson from
     fixing a bug), `decision-note` (a why-X-over-Y that doesn't warrant a
     full ADR in `docs/ADRs/`), `ci-failure` (a recurring CI failure and its
     cause), `persona-note` (an observation from a `persona-simulation` run
     not already filed as a GitHub issue).
   - **Edit in place** if it's an attribute or update of something that
     already has a page. Don't fork a near-duplicate file.
   - **Supersede, don't overwrite**, when a new source contradicts an
     existing claim rather than just extending it. Add `superseded_by:` to
     the old page's frontmatter pointing at the new one, keep the old page.
3. Before creating a page, check it actually *compresses* the source. If the
   concept is small enough that grepping the source directly would answer
   as fast, it isn't worth a page.
4. Copy `TEMPLATE.md` into `wiki/` for new pages. Fill in `type` (required)
   and the recommended fields, and place it under the most specific existing
   directory (create one if none fits). Every new or edited page must list
   what it was built from in its `# Citations` section, in whichever of the
   three channel forms applies: `sources/<path>`, a repo path (optionally
   `@<sha>`), or `#NNN` / a full issue-or-PR URL.
5. Cross-link related concepts with bundle-relative markdown links (a
   leading `/` resolves from `wiki/`, the bundle root).
6. Update every `index.md` between the new/changed file and `wiki/index.md`.
   Keep entries to one line each. If a section grows past ~20-30 entries,
   split it into its own sub-index and link it from the parent.
7. Append one entry to `wiki/log.md` (or the nearest sub-log), newest-first,
   ISO 8601 date, bold action prefix: `**Added**`, `**Updated**`, `**Fixed**`,
   `**Superseded**`.
8. Run `python scripts/validate.py` (from `wiki/`) before finishing -
   remember this only checks frontmatter shape on pages that exist, never
   cite a clean run as evidence that `sources/` ingestion is complete.

## Query - when asked a question about this bundle's knowledge

1. Read `wiki/index.md` (and any sub-indexes it points to) first and pick the
   handful of pages that actually look relevant. Don't scan the whole bundle.
2. Read only those pages and answer with citations to the specific files
   used.
3. Coverage tripwire: one quick grep of `sources/` for the topic's obvious
   keywords/filenames. If that turns up source paths the pages you read
   don't cite, say so alongside the answer. This is a single grep, not a
   full audit - that's Lint's job, and only for the `sources/` channel.
4. If answering required synthesis that isn't captured anywhere and is
   likely to be asked again, file it back as a new concept (see Ingest)
   instead of letting it disappear into the conversation.

## Lint - periodically, before believing "nothing to do", or when asked to clean up the bundle

1. Run `python scripts/validate.py` and fix anything it flags - this only
   checks frontmatter schema on files already inside `wiki/`; it has no
   knowledge of `sources/` and cannot detect missing ingestion.
2. **Source-coverage audit**, scoped to the `sources/` channel (the only
   fully enumerable one). Run this every single pass, unconditionally.
   Glob `sources/**/*` fresh, re-check `git status --short sources/`, and
   cross-reference every path against every page's `# Citations` section in
   both directions: every source cited by some page (else "uncited
   source"), every `sources/...` citation resolving to a real path (else
   "broken citation"). Report exact counts with the full list behind each.
3. Look for orphan concepts under `wiki/`: files no `index.md` links to.
4. Look for stale claims: pages whose source material has since changed but
   weren't updated (an optional `status: draft|verified|stale` frontmatter
   field, if used, makes this a lookup instead of a re-read).
5. Look for concepts that now contradict each other and reconcile or flag
   them.
6. Report findings. List every step-2 gap individually. Don't self-remediate
   a coverage gap here - flag it and hand it to Ingest or the user.
7. Never conclude "everything is already ingested" or "nothing to do" unless
   step 2's audit actually ran in this pass and produced that result with
   the counts shown. This conclusion only ever covers the `sources/`
   channel - it says nothing about the repo or issue/PR channels, which
   aren't exhaustively audited.

## Conventions

- `type` is the only required frontmatter field. Reuse an existing `type`
  string from elsewhere in the bundle rather than inventing a
  near-duplicate.
- `index.md` and `log.md` are reserved at every level under `wiki/`; every
  other `.md` file under `wiki/` (outside `sources/`, `scripts/`, `.claude/`)
  is a concept.
- Keep prose terse - this bundle is read by agents at least as often as
  humans.
- Plain ASCII hyphens only, no Unicode dashes - same rule as the rest of
  this repo (see root `CLAUDE.md`).
