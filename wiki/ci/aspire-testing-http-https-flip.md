---
type: ci-failure
title: An Aspire.Hosting.Testing bump flipped CreateHttpClient's default endpoint from http to https
description: Upgrading to Aspire.Hosting.Testing 13.4 changed the unqualified CreateHttpClient overload to prefer the https endpoint, breaking every IntegrationTests call with an SSL UntrustedRoot error against the backend's self-signed dev cert.
tags: [ci, aspire, testing, dependency-upgrade]
timestamp: 2026-07-16
---

# Schema

A transitive test-framework dependency bump can silently change a default (here: which endpoint an unqualified client-creation call resolves to) in a way that looks like an unrelated networking or certificate problem, not a version-bump regression. When one test fixture already carries an explicit workaround for a library default, check sibling fixtures for the same unqualified call before and after the next bump of that dependency - they can drift out of sync.

# Examples

`Aspire.Hosting.Testing` v13.4.6 changed the unqualified `CreateHttpClient("backend")` overload to prefer the `https` endpoint over `http`. `backend/tests/VisualTests/AspireFixture.cs` already worked around this by explicitly passing the `"http"` endpoint name; `backend/tests/IntegrationTests/IntegrationTestFixture.cs` had not been updated and still used the unqualified overload, so every `IntegrationTests` call failed with an SSL `UntrustedRoot` error because the backend readiness probe and all API calls hit the HTTPS Kestrel endpoint with its self-signed dev cert. The fix changed both call sites in `IntegrationTestFixture.cs` to `_app.CreateHttpClient("backend", "http")`.

# Citations

- commit `06c4fba` - fix: clear orphaned time slots when opportunity switches away from Waitlist (#565)
- `backend/tests/IntegrationTests/IntegrationTestFixture.cs`
- `backend/tests/VisualTests/AspireFixture.cs`
