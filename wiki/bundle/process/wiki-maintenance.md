---
type: "process"
title: "Keeping the project wiki self-building"
description: "Why validate.py is not evidence of completeness, how notes/ are append-only, and where the bundle came from."
tags:
  - wiki
  - autonomous
  - adr
timestamp: 2026-07-18
---

# What validate.py proves, and what it does not

`validate.py` scans only `bundle/`. It checks that each concept page starts with YAML frontmatter carrying a non-empty `type` and a non-empty `tags` list, and that a `# Related` heading exists. That is the entire contract. It has no notion of `notes/`, never reads the notes channel, and cannot detect that a source was never ingested. A green run means the existing pages are shaped correctly, nothing more. Reading a clean run as an "already ingested" signal drops sources silently, which is the exact failure the ingest workflow guards against.

# When "already ingested" is actually true

Completeness is earned by an audit, and only for `notes/`. A bare request ("ingest", "what's missing", "ingest everything") triggers a coverage pass: glob `notes/**/*` fresh, re-run `git status --short notes/` to catch mid-session additions, grep each path against every page's `# Citations`, and report concrete numbers - N found, M cited, K uncited, with the uncited list. "Already ingested" holds only at K=0, shown with those counts. The repo and issue/PR channels are not enumerable, so their coverage is on-request and best-effort; a full completeness claim across them is one the bundle cannot make.

# notes/ is append-only

Files in `notes/` are named `N-title.md` with an ever-incrementing counter - check the highest existing number before picking the next. Once added, a note is read-only by convention: it is never edited or deleted to correct it after the fact. A note that contradicts an earlier one is added as its own new file. The contradiction is resolved in `bundle/`, not in `notes/`: add `superseded_by: <path>` to the old concept page's frontmatter, pointing at the new page, and keep the old page. Supersession is recorded, never overwritten, so the earlier claim stays readable.

# Every page carries type, tags, and Related

A concept page needs a non-empty `type` and non-empty `tags`, both enforced by validate.py. `tags` do more than pass validation: the relatedness step greps the bundle for a new page's tags (plus 2-3 title and description keywords) to find pages it should connect to. Every page also needs a `# Related` section, and the link is bidirectional - when page A links page B, B's own `# Related` is edited to link back. validate.py rejects a missing `# Related` heading but cannot tell a grep-backed "None found." from a lazy one, so the grep has to actually run.

# Where the bundle came from

The wiki holds tacit knowledge that does not fit the formal surfaces: the arc42 architecture docs, the ADRs, and the per-directory `CLAUDE.md` files. The self-building LLM-wiki pattern (the Karpathy idea) was proposed in #701; #717 built it as an OKF (Open Knowledge Format) v0.1 bundle, the in-house storage convention now living under `wiki/`. It complements the formal docs and links out to them rather than restating their content.

# Related
- [autonomous-routines](/decisions/autonomous-routines.md) - the ingest/query/lint skills that operate this bundle are part of the same routine tooling
- [adr-tdr-index](/reference/adr-tdr-index.md) - the wiki complements the formal docs and must link rather than duplicate them

# Citations
- wiki/AGENTS.md:56-131
- wiki/notes/README.md
- #701
- #717
