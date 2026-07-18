# Project wiki - ingest, query, lint

Informal knowledge about Einsatzbereit, stored as an OKF (Open Knowledge
Format) bundle - a lightweight in-house storage convention, not an external
standard. It sits alongside `docs/` (formal, reviewed) and the per-directory
`CLAUDE.md` files (stable conventions); `README.md` covers how the three
differ. A coding agent working in this repo is the wiki's maintainer, not just
an editor: OKF fixes the storage layout, and the operations below keep the
bundle self-building. Follow them on every change to the bundle.

Paths in this file are relative to `wiki/`, the assumed working directory:
`scripts/validate.py` and `TEMPLATE.md` are scaffolding at the wiki root, the
bundle lives in `bundle/` (`bundle/index.md`, `bundle/log.md`, and the concept
pages), the loose-notes channel lives in `notes/`, and anything outside
`wiki/` keeps its repo-root prefix (`.claude/...`). The `/ingest`, `/query`,
`/lint` skills run from the repo root instead and spell every path in full
(`wiki/bundle/...`, `wiki/notes/...`).

See @WRITING_STYLE.md for the prose rule - it applies to every write, not just
ingest.

The `/ingest`, `/query`, `/lint` skills (`.claude/skills/` at the repo root)
are self-contained copies of the operations below; Claude Code should prefer
them over reading this file end to end. Other agents follow this file
directly. A change to the workflow updates both this file and the matching
skill.

## Input channels

A source can come from any of three places, all cited the same way once
distilled into a concept page. None ranks above the others as truth; they
differ only in whether they can be audited for completeness.

- **`notes/`** - loose notes and ideas about Einsatzbereit, dropped in by hand
  as plain Markdown or text. Read-only once added: a later note that
  contradicts an earlier one is added as its own note, and the supersession is
  recorded in `bundle/`, not by editing `notes/`. This is the one fully
  enumerable channel, so a bare "ingest everything" can be audited down to
  zero uncited files against it (see the coverage audit below). That property
  comes from the notes being hand-dropped and enumerable, nothing more.
- **The repo** - commits, code, hook scripts, `CLAUDE.md` files. Cited by
  repo-relative path, optionally `@<commit-sha>` when the point is what a
  specific commit did. Not enumerable, so coverage here is best-effort and
  on-request ("ingest the gotcha behind commit `<sha>`"), never a completeness
  claim.
- **GitHub issues and PRs** - cited by `#NNN` or full URL. Same on-request,
  best-effort coverage as the repo channel.

The bundle in `bundle/` is the output, not an input. Its root is
`bundle/index.md` / `bundle/log.md`, and every other `.md` under it is a
concept file. Everything at the wiki root (`README.md`, `AGENTS.md`,
`CLAUDE.md`, `TEMPLATE.md`, `WRITING_STYLE.md`, `requirements.txt`,
`scripts/`) and the `notes/` folder sit outside the bundle and are not scanned
by `scripts/validate.py`, which reads only `bundle/`.

## Ingest - when given a new source, or asked what's missing

The bundle is a compiled, compounding artifact, not a static pile of
documents. A false "already ingested" verdict breaks that: it drops a source
silently. Completeness is therefore only ever claimed for the enumerable
`notes/` channel, and only after the audit below actually runs - never from a
prior session, a memory note, or a clean `validate.py` run. `validate.py`
checks frontmatter shape on existing pages; it has no notion of `notes/` and
cannot detect a missing ingestion.

A single-source request names one path (under `notes/`, a commit SHA, or an
issue/PR ref) - run steps 1-9 on it. A bare request ("ingest", "what's
missing", "ingest everything") triggers a coverage audit of `notes/`, the only
channel that supports one:

- Glob `notes/**/*` fresh (files and dirs, not just top-level entries) and
  re-run `git status --short notes/` to catch anything added or staged
  mid-session.
- Grep each enumerated path against every concept page's `# Citations`
  section.
- Report concrete numbers - "N found, M cited, K uncited: `<list>`" - not a
  bare "looks complete". "Already ingested" holds only at K=0, shown with the
  counts.
- Run steps 1-9 on each uncited item in turn.

The repo and issue/PR channels are ingested on request, not audited this way;
a request for a full coverage claim across them is one the bundle cannot
answer.

1. Read the source in full before writing anything (the raw file under
   `notes/`, the commit/diff, or the issue/PR thread). Split a source too long
   for one read at its natural structure and ingest piece by piece, updating
   the same pages incrementally rather than holding the whole thing in
   context.
2. Decide, per concept touched:
   - **New page** only for a distinct entity/concept other pages would link
     to. Reuse a `type` already used in the bundle rather than inventing a
     near-duplicate. Starter types: `gotcha` (a lesson from fixing a bug),
     `decision-note` (a why-X-over-Y too small for a full ADR in
     `docs/ADRs/`), `ci-failure` (a recurring CI failure and its cause),
     `persona-note` (an observation from a `persona-simulation` run not
     already filed as a GitHub issue).
   - **Edit in place** for an attribute or update of something that already
     has a page. No near-duplicate fork.
   - **Supersede, don't overwrite**, when a source contradicts an existing
     claim rather than extending it: add `superseded_by:` to the old page's
     frontmatter pointing at the new one, and keep the old page.
3. Before creating a page, confirm it *compresses* the source. A concept small
   enough that grepping the source answers as fast isn't worth a page.
4. Copy `TEMPLATE.md` into `bundle/` for a new page. Fill in `type` and `tags`
   (both required) and the recommended fields, under the most specific
   existing directory in `bundle/` (create one if none fits). List what the
   page was built from in its `# Citations` section, in whichever channel form
   applies: `wiki/notes/<path>`, a repo path (optionally `@<sha>`), or `#NNN` /
   a full issue-or-PR URL. Citation paths are repo-root-relative, independent
   of the working directory.
5. **Relatedness check - mandatory, same rigor as the coverage audit.** A page
   standing alone when it didn't have to is the failure this catches:
   - Grep `bundle/**/*.md` for the page's `tags` and 2-3 obvious keywords from
     its title/description, before writing `# Related`, not from memory of
     what's probably in the bundle.
   - For every genuinely related page found (a real shared topic, not an
     incidental word), link both directions: the new page's `# Related` links
     to it, and that page's `# Related` gets edited to link back (the "edit in
     place" case from step 2, applied to the older page).
   - If nothing genuinely turns up, `# Related` still exists - write
     `# Related\nNone found.` rather than leaving it blank. `validate.py`
     rejects a missing `# Related` heading but can't tell a lazy "None found"
     from a real one; the grep has to happen.
6. Update every `index.md` between the new/changed file and `bundle/index.md`.
   One line per entry. Split a section past ~20-30 entries into its own
   sub-index linked from the parent.
7. Append one entry to `bundle/log.md` (or the nearest sub-log), newest-first,
   ISO 8601 date, bold action prefix: `**Added**`, `**Updated**`, `**Fixed**`,
   `**Superseded**`.
8. Run `python scripts/validate.py` (from `wiki/`) before finishing. It checks
   frontmatter shape and `# Related` presence on existing pages only - not
   evidence that `notes/` ingestion is complete.
9. Report which pages were created vs. updated, and which existing pages got a
   backlink in step 5 - a silent backlink is as easy to lose as a silent page
   edit.

## Query - when asked a question about this bundle's knowledge

1. Read `bundle/index.md` (and any sub-indexes it points to) first and pick
   the handful of pages that look relevant. No full-bundle scan.
2. Read those pages and answer with citations to the specific files used.
3. Coverage tripwire: one quick grep of `notes/` for the topic's obvious
   keywords/filenames. If it turns up source paths the pages read don't cite,
   say so alongside the answer. This is a single grep, not the full audit -
   that's Lint's job, and only for `notes/`.
4. If the answer needed synthesis that isn't captured anywhere and is likely
   to be asked again, file it back as a new concept (see Ingest, including the
   step 5 relatedness check) rather than letting it vanish into the
   conversation.

## Lint - periodically, before believing "nothing to do", or when asked to clean up the bundle

1. Run `python scripts/validate.py` and fix anything it flags. It checks
   frontmatter schema and `# Related` presence on files in `bundle/` only; it
   has no knowledge of `notes/` and cannot detect a missing ingestion.
2. **Source-coverage audit**, scoped to `notes/` (the only fully enumerable
   channel). Run it every pass. Glob `notes/**/*` fresh, re-check
   `git status --short notes/`, and cross-reference every path against every
   page's `# Citations` in both directions: every source cited by some page
   (else "uncited source"), every `wiki/notes/...` citation resolving to a
   real path (else "broken citation"). Report exact counts with the full list
   behind each.
3. **Relatedness audit.** For every concept page, compare its `tags` (and
   title/description keywords) against every other page's. Two pages sharing a
   real topic with no link in either `# Related` section is an **unlinked
   related pair** - report both paths and what they share. A `# Related`
   saying "None found" despite an obvious shared-tag match is a **false "none
   found"**, reported the same way. Report exact counts - "N concept pages, M
   candidate pairs share a tag, K of those have zero link either direction" -
   not a vague "looks connected".
4. Find orphan concepts under `bundle/`: files no `index.md` links to. Distinct
   from step 3's unlinked-but-indexed pairs - an orphan is in no index at all.
5. Find stale claims: pages whose source has since changed but weren't updated
   (an optional `status: draft|verified|stale` frontmatter field makes this a
   lookup instead of a re-read).
6. Find concepts that now contradict each other and reconcile or flag them.
7. Report findings, listing every step-2 gap and step-3 pair individually.
   Don't self-remediate a coverage or relatedness gap here - flag it for
   Ingest or the user (Ingest step 5 adds the missing link, both directions).
8. "Everything is already ingested" / "nothing to do" is valid only when steps
   2 and 3 ran this pass and produced that result with the counts shown. It
   covers `notes/` and the relatedness graph only - never the repo or issue/PR
   channels.

## Conventions

- `type` and `tags` (non-empty) are the required frontmatter fields. `tags` is
  what the relatedness check searches on, so an empty or missing `tags` list
  quietly breaks that mechanism for the page. Reuse an existing `type` rather
  than inventing a near-duplicate.
- `index.md` and `log.md` are reserved at every level under `bundle/`; every
  other `.md` file under `bundle/` is a concept.
- Every concept page has a `# Related` section (see Ingest step 5);
  `scripts/validate.py` enforces its presence, not its honesty.
- Keep prose terse - the bundle is read by agents at least as often as by
  humans.
- Plain ASCII hyphens only, no Unicode dashes - same rule as the rest of this
  repo (see root `AGENTS.md`).
