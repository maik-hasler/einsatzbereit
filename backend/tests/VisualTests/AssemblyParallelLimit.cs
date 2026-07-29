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
// suite. Capping at exactly Environment.ProcessorCount (as this used to)
// still starves the Aspire-hosted stack itself: dotnet.yml's visual-tests
// job runs on ubuntu-latest (4 vCPUs), and those same cores also have to
// service the stack (Postgres, Keycloak, backend API, frontend dev server)
// that every one of the N concurrent Playwright sessions is calling into,
// not just the N browser/axe-core processes - e.g.
// AccessibilityTests.OrgDashboardPage_PlacingAWidget_AsOlaf and
// EngagementManagementPage_AsOlaf both timed out waiting on the same
// GET /v1/organizations round trip in the same CI run (2026-07-28), the
// exact contention pattern AuthHelper.GoToOrgAppDashboardAsync's comment
// already predicted as the suite grew. Reserving one core for the stack
// itself is a structural fix for that class of flake, as opposed to the
// timeout bumps this file's sibling comments already flag as running out
// of headroom. Environment.ProcessorCount - 1 (floored at 1) rather than a
// hardcoded number so this still scales with whatever runner size CI
// happens to use, and with a larger local dev machine.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 1);
}
