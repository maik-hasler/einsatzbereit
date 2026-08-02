using TUnit.Core.Interfaces;

// Mitigates the flaky Respawner/Npgsql "Timeout during reading attempt"
// failures seen on PR #1597 (2026-08-02, two separate CI runs, four
// different tests across EngagementTests/CheckInAttemptLimiterTests - same
// signature both times, always inside IntegrationTestFixture.ResetDatabaseAsync's
// raw NpgsqlConnection, never in the test body itself). Every test class in
// this assembly shares one Aspire-hosted stack
// (ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)) -
// Postgres, Keycloak, the backend API, MinIO, Mailpit - all on the same
// runner. Most DB-touching classes already carry [NotInParallel("IntegrationDb")],
// which only serializes tests *within* that key; it does nothing to cap how
// many of the remaining classes (or the "IntegrationDb" group as a whole
// relative to them) run at once, so with no assembly-wide limit TUnit's
// default - unbounded concurrency, the .NET thread pool decides - can still
// pile enough concurrent HTTP calls onto the shared stack to starve Postgres
// of CPU on a standard CI runner, occasionally tipping a plain connection
// read over its timeout. This is the same root cause VisualTests hit first
// (see VisualTests/AssemblyParallelLimit.cs) for the same reason (one shared
// Aspire stack, no assembly-wide cap) - reusing its fix here rather than
// starting from Environment.ProcessorCount - 1 and re-discovering
// empirically that VisualTests already found insufficient.
[assembly: ParallelLimiter<IntegrationTestsParallelLimit>]

public sealed class IntegrationTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 2);
}
