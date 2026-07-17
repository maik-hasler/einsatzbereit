---
name: lint
description: Health-check the project wiki (wiki/) for rot - orphan pages, stale claims, contradictions, missing cross-references, frontmatter errors, uncited sources. Use periodically, or when the user asks to clean up, lint, or audit the wiki.
allowed-tools: Read, Grep, Glob, Edit, Write, Bash(python wiki/scripts/validate.py:*), Bash(git status:*)
---

## Lint

Paths here are relative to the repo root (Claude Code's working directory) -
e.g. `wiki/scripts/validate.py`, not `scripts/validate.py`.

The wiki is a compiled, compounding artifact, not a static pile of
documents. A false "nothing to do" verdict silently breaks that compounding
loop - so steps 2 and 3 below are mandatory, not bureaucratic overhead.

1. Run `python wiki/scripts/validate.py`; fix anything it flags. This only
   checks frontmatter schema and `# Related`-section presence on files
   already inside `wiki/` - it has no knowledge of `docs/notes/` and cannot
   detect missing ingestion.
2. **Source-coverage audit**, scoped to `docs/notes/` (the only fully
   enumerable channel - the repo and issue/PR channels aren't audited this
   way, see `wiki/AGENTS.md`). Run this in full, every single invocation.
   a. `Glob docs/notes/**/*` recursively for the live, current list.
   b. For each source path, grep `wiki/**/*.md` for a citing reference to it
      (the `docs/notes/...` string in a page's Citations section). Zero
      hits -> **uncited source** - report the full path.
   c. Check the reverse direction: grep `wiki/**/*.md` for `docs/notes/...`
      citation strings and confirm each still exists on disk. A citation
      pointing at a path that's gone -> **broken source citation** - report
      the full path and the citing page.
   d. Run `git status --short docs/notes/` to catch anything added,
      renamed, or moved since any earlier snapshot. Re-run it again right
      before declaring the audit done.
   e. Report exact counts for both directions - "X paths under
      docs/notes/, Y uncited, Z broken citations" - with the full list
      behind each count.
3. **Relatedness audit - the check this bundle previously skipped
   entirely.** For every concept page, compare its `tags` (and
   title/description keywords) against every other page's.
   a. Two pages sharing a real topic with no link between them in either
      `# Related` section -> **unlinked related pair** - report both page
      paths and what they appear to share.
   b. A page whose `# Related` section says "None found" despite an
      obvious shared-tag match with another page -> **false "none found"**
      - report it the same way a false "already ingested" would be.
   c. Report exact counts - "N concept pages, M candidate pairs share a
      tag, K of those pairs have zero link either direction" - a vague
      "looks reasonably connected" is not a valid conclusion, for the same
      reason it isn't one for source coverage.
4. Find orphan **pages**: concept files under `wiki/` that no `index.md` (at
   any level) links to. Separate from step 2's uncited **sources** and step
   3's unlinked-but-indexed **pairs**.
5. Find stale claims: pages whose `status` (if used) is `stale`, or whose
   content looks superseded by a newer concept.
6. Find contradictions between concepts written from different ingests.
7. Report findings. List every uncited source, broken citation, and
   unlinked pair from steps 2-3 individually. Don't self-remediate a
   coverage or relatedness gap here - flag it and hand it to `/ingest` or
   the user (ingest step 5 is what actually adds the missing link, in both
   directions).
8. Never conclude "everything is already ingested" or "nothing to do" unless
   steps 2 and 3 actually ran in this pass and produced that result with
   the counts shown. This conclusion only ever covers `docs/notes/` and the
   relatedness graph - say nothing about the repo or issue/PR channels.
