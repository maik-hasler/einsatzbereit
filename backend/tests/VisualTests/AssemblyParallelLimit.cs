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
// Cap concurrent tests to the machine's core count so CPU-bound work (axe-core
// scans, React commits, Chromium rendering) doesn't oversubscribe the runner
// and start missing the timing assertions scattered across this suite.
// Environment.ProcessorCount rather than a hardcoded number so this scales
// with whatever runner size CI happens to use, and with a larger local dev
// machine.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	public int Limit => Environment.ProcessorCount;
}
