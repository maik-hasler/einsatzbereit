using TUnit.Core.Interfaces;

// Root cause of a class of flaky VisualTests failures (e.g. #1339): with no
// parallelism limit anywhere in this project (AccessibilityTests alone has
// ~50 test methods, none tagged [NotInParallel]), TUnit's default - "every
// test is eligible to run concurrently, the .NET thread pool decides how many
// at once" - let a standard CI runner spin up 16+ concurrent Chromium/
// Playwright instances, each driving axe-core's CPU-heavy DOM scan, all
// against one shared Aspire-hosted stack (SharedType.PerTestSession). That
// sustained contention, not a brief blip, is what made individual tests
// intermittently time out waiting on UI state - a problem retrying a failed
// test can't fix, since a retry lands in the same still-contended run.
//
// Cap concurrent tests below the machine's core count so CPU-bound work
// (axe-core scans, React commits, Chromium rendering) doesn't oversubscribe
// the runner and start missing the timing assertions scattered across this
// suite. Capping at ProcessorCount (as this used to) still starves the
// Aspire-hosted stack itself: every core is claimed by a test's own
// Chromium/axe-core work, leaving none free for the backend/Postgres/
// Keycloak/frontend processes those tests are actually driving - e.g.
// AccessibilityTests.OrgDashboardPage_PlacingAWidget_AsOlaf and
// EngagementManagementPage_AsOlaf both timed out waiting on the same
// GET /v1/organizations round trip in the same CI run (2026-07-28). Reserving
// one core for the stack itself is a structural fix for that class of flake,
// as opposed to the timeout bumps this file's sibling comments already flag
// as running out of headroom. Environment.ProcessorCount rather than a
// hardcoded number so this scales with whatever runner size CI happens to
// use, and with a larger local dev machine.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 1);
}
