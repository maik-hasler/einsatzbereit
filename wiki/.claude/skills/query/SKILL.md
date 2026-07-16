---
name: query
description: Answer a question using the project wiki (wiki/) instead of general knowledge - search wiki/index.md first, read only the relevant concept pages, cite them, and file genuinely new synthesis back into the wiki. Use when the user asks a question this bundle should be able to answer.
argument-hint: [question]
---

## Query: $0

1. Read `wiki/index.md` (and any sub-indexes it points to) first and pick
   the handful of pages that actually look relevant. Don't scan the whole
   bundle.
2. Read only those pages and answer with citations to the specific files
   used.
3. Coverage tripwire: run one quick grep over `wiki/sources/` for the
   topic's obvious keywords/filenames. If it turns up source paths that
   aren't cited by any page read, say so explicitly alongside the answer -
   e.g. "note: `sources/<x>.md` looks relevant but isn't cited by any page I
   read; consider running `/wiki:ingest` on it." This is a single grep, not
   a full audit - that's Lint's job, and scoped to `wiki/sources/` only (the
   repo and issue/PR channels aren't exhaustively checkable this way).
4. If answering required synthesis that isn't captured anywhere and is
   likely to be asked again, file it back as a new concept (new page vs.
   edit in place vs. supersede - same rules as ingest) instead of letting it
   disappear into the conversation.
