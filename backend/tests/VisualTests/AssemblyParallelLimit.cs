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
//
// Reserving just one core still wasn't enough: on 2026-07-29, three
// AccessibilityTests methods (OrgDashboardPage_PlacingAWidget_AsOlaf,
// EngagementManagementPage_AsOlaf, OrganizationSettingsPage_
// EditModeValidationError_AsOlaf - the single heaviest class in the suite,
// every method paying for a Page.RunAxe() DOM scan on top of the Chromium
// work every other test already does) hit this limit's cap of 3 concurrently
// and all three sat in the runner's "[slow] still running after 1m 00s" log
// at once; one blew past a 30s Playwright action timeout on a plain input
// fill (no network call involved), meaning it was CPU-starved, not
// backend-starved. The obvious-looking fix - give AccessibilityTests its own,
// tighter [ParallelLimiter<T>] at the class level on top of this assembly-wide
// one - does NOT work: verified empirically (throwaway TUnit project, two
// classes, one assembly-level limiter plus one class-level limiter on the
// second class, tests recording their own peak concurrency) that when both an
// assembly-level and a class-level ParallelLimiter apply to the same test,
// TUnit 1.34.5 honors only the assembly-level one - the class-level cap is
// silently ignored, in both directions (a looser assembly limit lets the
// class exceed its own tighter one; a tighter assembly limit overrides a
// looser class one). A class-level-only fix would have shipped as a
// no-op that still passes the build. Reduce the shared limit itself instead -
// this does cost the whole suite a slightly lower ceiling, not just the
// heaviest class, but it's the one lever that's actually proven to work.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 2);
}
