---
name: ingest
description: Ingest a new source into the project wiki (wiki/) - read it, create or update the concept pages it touches, update indexes, and log the change. Use when the user drops a note into docs/notes/, or asks to file/ingest/add something to the wiki, or names a commit/issue/PR to distill into it.
argument-hint: [path-under-docs/notes/ | commit-sha | issue-or-PR ref | blank for a docs/notes/ coverage audit]
---

## Ingest $ARGUMENTS

Paths here are relative to the repo root (Claude Code's working directory) -
e.g. `wiki/scripts/validate.py`, not `scripts/validate.py`.

Why this matters: the wiki is a compiled, compounding artifact, not a static
pile of documents. A false "nothing to do" / "already ingested" verdict
silently breaks that compounding loop. The checks below are the mechanism
that makes "nothing to do" a falsifiable conclusion instead of a guess, and
they are not optional for the `docs/notes/` channel.

Three input channels feed this wiki (see `../../../wiki/AGENTS.md`):

- **`docs/notes/`** - loose notes dropped by hand. Fully enumerable -
  audited to strict K=0 accounting below.
- **The repo** - commits, code, hook scripts, `CLAUDE.md` files. Ingested on
  request (`$ARGUMENTS` names a path or commit SHA), not exhaustively audited.
- **GitHub issues/PRs** - ingested on request (`$ARGUMENTS` names `#NNN` or a URL),
  not exhaustively audited either.

Pick a mode before doing anything else:

- **Mode A - docs/notes/ coverage audit**: `$ARGUMENTS` is empty, a glob/wildcard,
  or a vague phrase ("ingest", "what's missing", "ingest everything").
  Scoped to `docs/notes/` only - never claim this covers the repo or
  issue/PR channels too.
- **Mode B - single-source ingest**: `$ARGUMENTS` names one specific existing path
  under `docs/notes/`, a commit SHA, or an issue/PR ref.

### Mode A - docs/notes/ coverage audit

`scripts/validate.py` passing is not evidence of completeness here - it only
checks frontmatter shape on pages that already exist; it says nothing about
what's missing from `docs/notes/`.

0. Glob `docs/notes/**/*` recursively - files and dirs, not just top-level
   entries. This step must run and its output must be visible before any
   completeness statement.
0b. For each path returned, grep `wiki/**/*.md` for a reference to it - in
    particular each page's `# Citations` section. Zero hits = "uncited."
0c. Run `git status --short docs/notes/` to catch anything added or
    staged mid-session. Re-run this again immediately before the final
    report, not only at the start.
0d. Report concrete numbers: "Found N paths under docs/notes/, M already
    cited, K uncited: `<list>`." Prose like "looks complete" with no
    accompanying counts is not a valid output. "Already ingested" is only
    a valid conclusion when K=0, shown with the numbers.
0e. For any uncited source that's large/dense or a whole new top-level
    folder, ask the user how deep to go (full synthesis / partial
    enrichment / stub-with-pointer / catalog-only) rather than deciding
    unilaterally.

Then work through the K uncited sources one at a time using Mode B below.

### Mode B - single-source ingest

1. Read the source in full before writing anything - the raw file under
   `docs/notes/`, the commit/diff, or the issue/PR thread. For a long
   source that doesn't fit in one read, split it into smaller pieces at
   whatever natural structure it has and ingest piece by piece, updating the
   same wiki pages incrementally.
2. Decide, per concept touched:
   - **New page** only if it's a distinct entity/concept other pages would
     link to. Check `type` values already used in the bundle before
     inventing one - reuse an existing type. Starters for this bundle:
     `gotcha`, `decision-note`, `ci-failure`, `persona-note`.
   - **Edit in place** if it's an attribute or update of something that
     already has a page. Don't fork a near-duplicate file.
   - **Supersede, don't overwrite**, when this source contradicts an
     existing claim rather than just extending it. Add `superseded_by:` to
     the old page's frontmatter pointing at the new one, keep the old page.
3. Before creating a page, check it actually *compresses* the source. If
   grepping the source directly would answer as fast, it isn't worth a page.
4. Copy `wiki/TEMPLATE.md` for new pages. Fill in `type` and `tags` (both
   required) and the recommended fields, place it under the most specific
   existing directory (create one if none fits). Every new or edited page
   lists what it was built from in `# Citations`: `docs/notes/<path>`, a
   repo path (optionally `@<sha>`), or `#NNN` / a full issue-or-PR URL.
5. **Relatedness check - mandatory, same rigor as Mode A's coverage audit.**
   A page that stands alone when it didn't have to is the failure mode this
   step exists to catch:
   - Grep `wiki/**/*.md` for this page's `tags` values and 2-3 obvious
     keywords from its title/description, before writing `# Related`, not
     from memory of what's "probably" in the bundle.
   - For every genuinely related page found (real shared topic, not an
     incidental word match), link it in **both directions**: this page's
     `# Related` section links to it, and that existing page gets edited in
     place to link back.
   - If nothing genuinely turns up, `# Related` still must exist - write
     `# Related\nNone found.` rather than leaving it blank. `validate.py`
     rejects a missing `# Related` heading but can't tell a lazy "None
     found" from a real one - the grep has to actually happen.
6. Update every `index.md` between the new/changed file and `wiki/index.md`.
   One line per entry. Split a section past ~20-30 entries into its own
   sub-index.
7. Append one entry to `wiki/log.md` (or the nearest sub-log), newest-first,
   ISO 8601 date, bold action prefix: `**Added**`, `**Updated**`, `**Fixed**`,
   `**Superseded**`.
8. Run `python wiki/scripts/validate.py`. This only checks frontmatter shape
   and `# Related` presence on existing pages - never cite it as evidence
   that `docs/notes/` ingestion is complete; that's Mode A's job.
9. Report which pages were created vs. updated, and which existing pages got
   a backlink added as part of step 5 - a silent backlink is as easy to lose
   track of as a silent page edit.
