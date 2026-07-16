---
type: ci-failure
title: CI bans em/en dashes anywhere in the repo
description: A dedicated job greps the whole repo for U+2013/U+2014 and fails the build on any match - not just in prose, in any file.
tags: [ci, lint, style]
timestamp: 2026-07-16
---

# Schema

Text pasted from an LLM, word processor, or many websites often carries real Unicode en/em dash characters instead of a plain hyphen. This repo's CI treats that as a build-breaking error anywhere in the tree, code included - the fix is always the same: replace with a plain ASCII hyphen (`-`), or restructure the sentence.

# Examples

The `ban-typographic-dashes` job in `.github/workflows/lint.yml` runs `grep -rPnI '[\x{2013}\x{2014}]' .` and exits 1 on any match, on every push to `main` and every pull request. The error it prints is exactly: "Em/en dashes found - use plain ASCII hyphens (-) instead." There are no path exclusions, so it applies to code, docs, and config alike.

# Citations

- `.github/workflows/lint.yml`
- `CLAUDE.md` (Key Conventions section)
