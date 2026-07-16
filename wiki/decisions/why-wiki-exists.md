---
type: decision-note
title: Why the Einsatzbereit wiki exists
description: Issue #701 proposed this wiki/ bundle to capture informal knowledge that doesn't fit arc42 docs, ADRs, or CLAUDE.md files; several of its questions are still open.
resource: https://github.com/maik-hasler/einsatzbereit/issues/701
tags: [meta, wiki, issue-701]
timestamp: 2026-07-16
status: draft
---

# Schema

A repo can have solid formal documentation (architecture docs, ADRs, per-directory convention files) and still lose informal knowledge that doesn't fit any of those shapes: a gotcha learned while fixing a bug, a why-X-over-Y call that isn't worth a full ADR, a recurring CI failure cause, a persona-simulation finding that never became an issue. That's the gap this wiki fills - see `wiki/README.md` for how it relates to `docs/` and the `CLAUDE.md` files.

# Examples

Issue #701, opened 2026-07-15 and labeled `needs-decision`, titled "Idea: repo-included LLM wiki (Karpathy pattern) for einsatzbereit itself", proposed exactly this. It raised five open questions: the wiki's relationship to existing docs (replace/duplicate/complement), its location (top-level directory vs. inside `docs/`), its maintenance model (automatic side effect of other work vs. periodic pass vs. manual-only), its format (Markdown vs. AsciiDoc to match `docs/`), and its scope (architecture/domain knowledge only, or also CI issues and persona-sim findings).

This first pass answered location (top-level `wiki/`, per the repo owner directly) and format (Markdown, matching the OKF pattern this bundle is based on rather than `docs/`'s AsciiDoc). Maintenance model and final scope boundaries are still open - this bundle currently has no CI wiring and no hook into `issue-triage`/`persona-simulation`, by deliberate choice for this first pass.

# Citations

- `#701` https://github.com/maik-hasler/einsatzbereit/issues/701
