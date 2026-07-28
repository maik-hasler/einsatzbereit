using TUnit.Core.Interfaces;

// Root cause of a class of flaky VisualTests failures (e.g. #1339):
// AssemblyRetryPolicy.cs's [assembly: Retry(2)] already documents that a
// contended CI runner makes individual tests intermittently time out waiting
// on UI state - but retrying doesn't help when the contention is sustained
// for the whole run, not a brief blip: with no parallelism limit anywhere in
// this project (AccessibilityTests alone has ~50 test methods, none tagged
// [NotInParallel]), TUnit's default - "every test is eligible to run
// concurrently, the .NET thread pool decides how many at once" - let a
// standard CI runner spin up 16+ concurrent Chromium/Playwright instances,
// each driving axe-core's CPU-heavy DOM scan, all against one shared
// Aspire-hosted stack (SharedType.PerTestSession). A retry lands in the same
// still-contended run and just as easily times out again.
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
// GET /v1/organizations round trip in the same CI run (2026-07-28), the
// exact contention pattern AuthHelper.GoToOrgAppDashboardAsync's comment
// already predicted as the suite grew. Reserving one core for the stack
// itself is a structural fix for that class of flake, as opposed to the
// timeout bumps this file's sibling comments already flag as running out of
// headroom. Environment.ProcessorCount rather than a hardcoded number so
// this scales with whatever runner size CI happens to use, and with a larger
// local dev machine.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	// Floor of 2 rather than 1: a standard 2-core GitHub runner would otherwise
	// reduce to a single concurrent test, serialising a ~207-test browser suite
	// for no reliability gain (the contention this guards against needs several
	// concurrent Chromium instances to exist in the first place).
	public int Limit => Math.Max(2, Environment.ProcessorCount - 1);
}
