---
type: ci-failure
title: VisualTests flakiness is handled with an assembly-level Retry(2), not by removing the CI gate
description: A recurring transient timeout under a shared Aspire test stack was blocking the staging deploy gate; the fix retries the failed test method rather than loosening or removing the gate.
tags: [ci, testing, flaky-tests]
timestamp: 2026-07-16
---

# Schema

When end-to-end tests share one hosted stack per test session under a contended CI runner, individual tests can intermittently time out on UI-state waits (a dialog closing, a save-then-navigate read) without any actual application defect. The narrow fix is a bounded per-test retry, not raising timeouts globally or unblocking the deploy gate from the suite - a genuine regression should still fail every attempt and still fail the suite.

# Examples

Around 61 `VisualTests` classes share one Aspire-hosted stack per test session (`SharedType.PerTestSession`). One test had flaked this way on nearly every recent release-candidate run, repeatedly blocking the staging deploy gate on a transient failure rather than a real regression. The fix is a 10-line file, `backend/tests/VisualTests/AssemblyRetryPolicy.cs`, containing only `[assembly: Retry(2)]` plus an explanatory comment - it re-runs only the failed test method, up to 2 extra attempts, against the same shared stack.

Relates to issue #623, which had proposed retry-on-failure as one option; this applied it narrowly at the test level rather than removing the deploy gate.

# Citations

- commit `5b9f138` - fix: retry flaky VisualTests before failing CI (#636)
- `backend/tests/VisualTests/AssemblyRetryPolicy.cs`
