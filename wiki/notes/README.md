# wiki/notes/

Loose notes and ideas about Einsatzbereit as plain Markdown or text, named
`N-title.md` with an ever-incrementing counter (e.g. `1-idea.md`,
`2-other-idea.md`) - check the highest existing number in this folder before
picking the next one. Read-only by convention once added: a file here is not
edited or deleted to "correct" it after the fact. A later note that
contradicts an earlier one is added as its own note, and `/ingest` records the
supersession in `../bundle/`, not here.

Unlike `docs/Architecture/`, `docs/ADRs/`, and `docs/TDRs/` - formal, reviewed
AsciiDoc - this folder is informal and unreviewed: raw material, not a
documentation surface in its own right.

This is one of three input channels for the project wiki; `../README.md` and
`../AGENTS.md` describe the other two (the repo itself, GitHub issues/PRs).

A note added here is picked up by `/ingest <path>`, or by a bare `/ingest`
that runs a full coverage pass over this folder, which reads it and distills it
into `../bundle/`.
