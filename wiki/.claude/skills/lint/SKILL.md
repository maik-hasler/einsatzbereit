---
name: lint
description: Health-check the project wiki (wiki/) for rot - orphan pages, stale claims, contradictions, missing cross-references, frontmatter errors, uncited sources. Use periodically, or when the user asks to clean up, lint, or audit the wiki.
allowed-tools: Read Bash(python wiki/scripts/validate.py) Bash(git status --short wiki/sources/) Grep Glob Edit Write
---

## Lint

The wiki is a compiled, compounding artifact, not a static pile of
documents. A false "nothing to do" verdict silently breaks that compounding
loop - so step 2 below is mandatory, not bureaucratic overhead.

1. Run `python wiki/scripts/validate.py`; fix anything it flags. This only
   checks frontmatter schema on files already inside `wiki/` - it has no
   knowledge of `wiki/sources/` and cannot detect missing ingestion.
2. **Source-coverage audit**, scoped to `wiki/sources/` (the only fully
   enumerable channel - the repo and issue/PR channels aren't audited this
   way, see `AGENTS.md`). Run this in full, every single invocation.
   a. `Glob wiki/sources/**/*` recursively for the live, current list.
   b. For each source path, grep `wiki/**/*.md` for a citing reference to it
      (the `sources/...` string in a page's Citations section). Zero hits ->
      **uncited source** - report the full path.
   c. Check the reverse direction: grep `wiki/**/*.md` for `sources/...`
      citation strings and confirm each still exists on disk. A citation
      pointing at a path that's gone -> **broken source citation** - report
      the full path and the citing page.
   d. Run `git status --short wiki/sources/` to catch anything added,
      renamed, or moved since any earlier snapshot. Re-run it again right
      before declaring the audit done.
   e. Report exact counts for both directions - "X paths under
      wiki/sources/, Y uncited, Z broken citations" - with the full list
      behind each count.
3. Find orphan **pages**: concept files under `wiki/` that no `index.md` (at
   any level) links to. Separate from step 2's uncited **sources**.
4. Find stale claims: pages whose `status` (if used) is `stale`, or whose
   content looks superseded by a newer concept.
5. Find contradictions between concepts written from different ingests.
6. Report findings. List every uncited source and broken citation from
   step 2 individually. Don't self-remediate a coverage gap here - flag it
   and hand it to `/wiki:ingest` or the user.
7. Never conclude "everything is already ingested" or "nothing to do" unless
   step 2's audit actually ran in this pass and produced that result with
   the counts shown. This conclusion only ever covers `wiki/sources/` - say
   nothing about the repo or issue/PR channels.
