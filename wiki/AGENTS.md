# Project wiki - ingest, query, lint

Informal knowledge about Einsatzbereit, stored as an OKF (Open Knowledge
Format) bundle - a lightweight in-house storage convention, not an external
standard. See `README.md` and
[issue #701](https://github.com/maik-hasler/einsatzbereit/issues/701) for why
this exists alongside `docs/` and the per-directory `CLAUDE.md` files. If you
are a coding agent working in this repo, you are the wiki's maintainer, not
just an editor. OKF fixes only the storage layout; the operations below
restore the self-building behavior of the "LLM wiki" pattern this format is
based on. Follow them whenever you touch this bundle.

This file assumes your working directory is `wiki/` (the bundle root): paths
like `scripts/validate.py` and `TEMPLATE.md` are bundle-relative, while paths
outside the bundle carry their repo-root prefix (`docs/notes/...`,
`.claude/...`). The `/ingest`, `/query`, `/lint` skills instead run from the
repo root and spell every path `wiki/...`.

See @WRITING_STYLE.md for the prose rule - applies to every write, not just
ingest.

If you're Claude Code, prefer the dedicated skills over re-reading this file
end to end: `/ingest`, `/query`, `/lint` (`.claude/skills/` at the repo
root). They're self-contained copies of the operations below. Other agents
should follow this file directly. If you change the workflow, update both
this file and the matching skill.

## Three input channels, one bundle

- **`docs/notes/`** (repo-root relative; `../docs/notes/` if your cwd is
  `wiki/`) - your own loose notes and ideas about Einsatzbereit, dropped by
  hand as plain Markdown or text. Immutable once added: read from it, never
  edit or delete anything in it. Fully enumerable, so this is the one
  channel the coverage-audit rules below hold to strict K=0 accounting -
  that guarantee comes from being hand-dropped and enumerable, not from
  living inside `wiki/`, and holds just as well at this address.
- **The repo itself** - commits, code, hook scripts, existing `CLAUDE.md`
  files. Cite directly by repo-relative path (optionally `@<commit-sha>`
  when the point is what a specific commit did). Not exhaustively
  enumerable the way `docs/notes/` is, so treat coverage here as best-effort /
  on-request ("ingest recent gotchas from the last N commits", "ingest
  issue #x") rather than something a lint pass can claim complete.
- **GitHub issues and PRs** - cited directly by `#NNN` or full URL. Same
  best-effort coverage caveat as the repo channel.
- **`wiki/`** - the OKF bundle itself. Its root is `wiki/index.md` /
  `wiki/log.md`; everything else under it that isn't `scripts/` is a
  concept file or directory. You own this layer entirely.

Everything else at `wiki/`'s own root (`README.md`, `AGENTS.md`, `CLAUDE.md`,
`TEMPLATE.md`, `WRITING_STYLE.md`, `requirements.txt`, `scripts/`)
is repo scaffolding - not part of the bundle, not scanned by
`scripts/validate.py`.

## Ingest - when given a new source, or asked what's missing

The wiki is a compiled, compounding artifact, not a static pile of documents
(this pattern is [Andrej Karpathy's LLM-wiki
idea](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)). A
false "already ingested" verdict silently breaks that compounding loop, so
treat completeness as something you prove every time for the `docs/notes/`
channel specifically, never something you assume from a prior session, a
memory note, or a clean `validate.py` run - `validate.py` only checks
frontmatter shape on pages that already exist; it has no notion of
`docs/notes/` and cannot detect missing ingestion.

Two modes for the `docs/notes/` channel - pick one before doing anything else:

- **Targeted**: the request names one specific existing path under
  `docs/notes/`. Run steps 1-9 below on it.
- **Coverage audit**: the request is bare ("ingest", "what's missing",
  "ingest everything") or names no specific path. Never skip straight to
  "everything's covered" here. Glob `docs/notes/**/*` fresh (files and dirs,
  not just top-level entries), re-run `git status --short docs/notes/` to
  catch anything added or staged mid-session, and grep each enumerated path
  against every wiki page's
  `# Citations` section. Report concrete numbers - "N found, M cited, K
  uncited: `<list>`" - a bare "looks complete" is not a valid conclusion.
  Then run Targeted mode on each uncited item in turn.

For the repo and issue/PR channels, ingest on request rather than trying to
audit them exhaustively - e.g. "ingest the gotcha behind commit `<sha>`" or
"ingest what issue #x settled." Say so explicitly if asked for a full
coverage claim across these two channels; it isn't one this bundle can make.

1. Read the source in full before writing anything (the raw file under
   `docs/notes/`, the commit/diff, or the issue/PR thread). For a long source
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
4. Copy `TEMPLATE.md` into `wiki/` for new pages. Fill in `type` and `tags`
   (both required) and the recommended fields, and place it under the most
   specific existing directory (create one if none fits). Every new or
   edited page must list what it was built from in its `# Citations`
   section, in whichever of the three channel forms applies:
   `docs/notes/<path>`, a repo path (optionally `@<sha>`), or `#NNN` / a
   full issue-or-PR URL.
5. **Relatedness check - mandatory, not optional, same rigor as the
   coverage audit above.** A page that stands alone when it didn't have to
   is exactly the failure mode this step exists to catch:
   - Grep `wiki/**/*.md` for the new/edited page's `tags` values and 2-3
     obvious keywords from its title/description. Do this before writing
     the `# Related` section, not from memory of what's "probably" in the
     bundle.
   - For every existing page that comes up genuinely related (shares a
     real topic, not just an incidental word), add a bundle-relative link
     in **both directions**: the new page's `# Related` section links to
     it, and that existing page's `# Related` section gets updated to link
     back (edit it in place - this is exactly the "edit in place" case from
     step 2, applied to the older page instead of the new one).
   - If the grep genuinely turns up nothing, the `# Related` section still
     must exist - write `# Related\nNone found.` rather than leaving it
     blank or skipping it. `scripts/validate.py` rejects a concept page
     with no `# Related` heading at all, but it cannot tell a lazy "None
     found" from a genuine one - the grep has to actually happen.
6. Update every `index.md` between the new/changed file and `wiki/index.md`.
   Keep entries to one line each. If a section grows past ~20-30 entries,
   split it into its own sub-index and link it from the parent.
7. Append one entry to `wiki/log.md` (or the nearest sub-log), newest-first,
   ISO 8601 date, bold action prefix: `**Added**`, `**Updated**`, `**Fixed**`,
   `**Superseded**`.
8. Run `python scripts/validate.py` (from `wiki/`) before finishing -
   remember this only checks frontmatter shape and `# Related` presence on
   pages that exist, never cite a clean run as evidence that `docs/notes/`
   ingestion is complete.
9. Report which pages were created vs. updated, and which existing pages got
   a backlink added as part of step 5 - a silent backlink is as easy to lose
   track of as a silent page edit.

## Query - when asked a question about this bundle's knowledge

1. Read `wiki/index.md` (and any sub-indexes it points to) first and pick the
   handful of pages that actually look relevant. Don't scan the whole bundle.
2. Read only those pages and answer with citations to the specific files
   used.
3. Coverage tripwire: one quick grep of `docs/notes/` for the topic's
   obvious keywords/filenames. If that turns up source paths the pages you
   read don't cite, say so alongside the answer. This is a single grep, not
   a full audit - that's Lint's job, and only for the `docs/notes/` channel.
4. If answering required synthesis that isn't captured anywhere and is
   likely to be asked again, file it back as a new concept (see Ingest,
   including the step 5 relatedness check) instead of letting it disappear
   into the conversation.

## Lint - periodically, before believing "nothing to do", or when asked to clean up the bundle

1. Run `python scripts/validate.py` and fix anything it flags - this only
   checks frontmatter schema and `# Related`-section presence on files
   already inside `wiki/`; it has no knowledge of `docs/notes/` and cannot
   detect missing ingestion.
2. **Source-coverage audit**, scoped to the `docs/notes/` channel (the only
   fully enumerable one). Run this every single pass, unconditionally.
   Glob `docs/notes/**/*` fresh, re-check `git status --short docs/notes/`,
   and cross-reference every path against every page's `# Citations` section in
   both directions: every source cited by some page (else "uncited
   source"), every `docs/notes/...` citation resolving to a real path (else
   "broken citation"). Report exact counts with the full list behind each.
3. **Relatedness audit - the check this bundle previously skipped entirely.**
   For every concept page, compare its `tags` (and title/description
   keywords) against every other page's. Two pages sharing a real topic but
   with no link between them in either `# Related` section is a finding,
   same class as an uncited source: report it as an **unlinked related
   pair**, both page paths, and what they appear to share. Report exact
   counts - "N concept pages, M candidate pairs share a tag, K of those
   pairs have zero link either direction" - a vague "looks reasonably
   connected" is not a valid conclusion, for the same reason it isn't one
   for source coverage. A page whose `# Related` section says "None found"
   despite an obvious shared-tag match is a **false "none found"** and gets
   reported the same way a false "already ingested" would.
4. Look for orphan concepts under `wiki/`: files no `index.md` links to.
   Separate from step 3's unlinked-but-otherwise-findable pairs - an orphan
   isn't in any index at all.
5. Look for stale claims: pages whose source material has since changed but
   weren't updated (an optional `status: draft|verified|stale` frontmatter
   field, if used, makes this a lookup instead of a re-read).
6. Look for concepts that now contradict each other and reconcile or flag
   them.
7. Report findings. List every step-2 gap and every step-3 unlinked pair
   individually. Don't self-remediate a coverage or relatedness gap here -
   flag it and hand it to Ingest or the user (Ingest step 5 is what actually
   adds the missing link, in both directions).
8. Never conclude "everything is already ingested" or "nothing to do" unless
   steps 2 and 3 actually ran in this pass and produced that result with the
   counts shown. This conclusion only ever covers the `docs/notes/` channel
   and the relatedness graph - it says nothing about the repo or issue/PR
   channels, which aren't exhaustively audited.

## Conventions

- `type` and `tags` (non-empty) are the required frontmatter fields - `tags`
  is what the relatedness check searches on, so an empty or missing `tags`
  list quietly breaks that mechanism for the page. Reuse an existing `type`
  string from elsewhere in the bundle rather than inventing a
  near-duplicate.
- `index.md` and `log.md` are reserved at every level under `wiki/`; every
  other `.md` file under `wiki/` (outside `scripts/`) is a concept.
- Every concept page has a `# Related` section (see Ingest step 5) -
  `scripts/validate.py` enforces its presence, not its honesty.
- Keep prose terse - this bundle is read by agents at least as often as
  humans.
- Plain ASCII hyphens only, no Unicode dashes - same rule as the rest of
  this repo (see root `AGENTS.md`).
