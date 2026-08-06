using TUnit.Core.Interfaces;

// All VisualTests classes share one Aspire-hosted stack (SharedType.PerTestSession);
// without a cap, TUnit runs every test concurrently and CPU contention among the
// Playwright/axe-core sessions (not backend slowness) causes intermittent timeouts
// that retries can't fix. Environment.ProcessorCount - 2 rather than ProcessorCount:
// the same cores also have to service the stack itself (Postgres, Keycloak, API,
// frontend dev server) that every concurrent session calls into, so headroom must
// stay reserved for it. ProcessorCount rather than a hardcoded number so this scales
// with whatever runner or dev machine it runs on.
//
// Do not add a class-level [ParallelLimiter<T>] (e.g. on AccessibilityTests) expecting
// it to layer a tighter cap on top of this one: TUnit 1.34.5 honors only the
// assembly-level limiter when both apply to the same test, silently ignoring the
// class-level one (verified empirically). Lower this shared limit instead if a
// particular suite needs more headroom.
[assembly: ParallelLimiter<VisualTestsParallelLimit>]

public sealed class VisualTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 2);
}
