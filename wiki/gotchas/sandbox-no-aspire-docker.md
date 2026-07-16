---
type: gotcha
title: Claude Code's web sandbox cannot run Aspire/Docker, even when docker info succeeds
description: The Aspire AppHost, IntegrationTests (Testcontainers), and VisualTests (Aspire + Playwright) all need real container networking that isn't available in a Claude Code web/cloud session; DCP orchestration fails even when docker info reports success.
tags: [sandbox, aspire, docker, testing]
timestamp: 2026-07-16
---

# Schema

`docker info` succeeding in a sandboxed environment doesn't mean container *networking* works - Aspire's DCP orchestration can still fail even though the daemon itself responds. Anything that needs a real Aspire-hosted stack (the AppHost, `IntegrationTests` via Testcontainers, `VisualTests` via Aspire + Playwright) has to be verified some other way in that environment.

# Examples

In this repo, the documented workaround is to verify locally with `dotnet build` plus the two test projects that don't need Docker (`Application.UnitTests`, `ArchitectureTests`), rely on CI's `dotnet.yml` to run the full suite (including `IntegrationTests`/`VisualTests`) on a real runner, and for anything user-visible, use the release-candidate-plus-live-staging Playwright flow instead of a local dev server. The same section also notes that direct pushes to `main` are blocked by the git proxy in this environment (working-branch only), so work always goes through a `claude/...` branch and a PR.

# Citations

- `CLAUDE.md` (Sandbox Limitations section)
