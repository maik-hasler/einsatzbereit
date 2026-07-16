---
type: ci-failure
title: Indentation is tabs by default; only JSON, YAML, and Markdown override to spaces
description: .editorconfig makes tabs the default for every file type, including shell scripts, AsciiDoc, and PlantUML; the editorconfig CI job enforces it, with the three NSwag-generated files and Markdown exempted from the check.
tags: [ci, editorconfig, style]
timestamp: 2026-07-16
---

# Schema

Assuming spaces-by-default (a common habit from JS/TS-only projects) fails CI here for anything outside the specific overrides. Check `.editorconfig`'s per-filetype rules before assuming either way, rather than copying whatever convention a specific file type used elsewhere.

# Examples

`.editorconfig`'s `[*]` rule sets `indent_style = tab`, `tab_width = 4` as the repo-wide default - shell scripts, AsciiDoc (`.adoc`), and PlantUML (`.puml`) all inherit it. `[*.{js,ts,jsx,tsx,css}]` overrides to `tab_width = 2` (still tabs, narrower, matching Prettier's `tabWidth: 2`). `[*.json]` and `[*.{yaml,yml}]` override to `indent_style = space` (JSON per ecosystem convention; YAML because tabs cause parser errors), and `[*.md]` overrides to `indent_style = space` and disables `trim_trailing_whitespace` (code blocks and list continuations need varying space indentation).

`.editorconfig-checker.json` excludes `LICENSE`, `*.md`, `ApiClient.cs`, `api-client.ts`, and `openapi-v1.json` from the check - the NSwag-generated files and Markdown are exempt. The `editorconfig` job in `.github/workflows/lint.yml` runs `editorconfig-checker@6.1.1` against this config, authenticated with `GITHUB_TOKEN` because the unauthenticated GitHub API rate limit (shared across the runner's IP) intermittently 403s the binary download otherwise. `docs/CLAUDE.md` adds that AsciiDoc paragraphs should stay on one unwrapped line rather than being hand-wrapped with space-indented continuations, and PlantUML note/legend blocks shouldn't be indented with spaces, since both inherit the tab default.

# Citations

- `.editorconfig`
- `.editorconfig-checker.json`
- `.github/workflows/lint.yml`
- `CLAUDE.md` (Key Conventions section)
- `docs/CLAUDE.md` (Format section)
