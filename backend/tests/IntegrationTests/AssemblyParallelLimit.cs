using TUnit.Core.Interfaces;

// Every test class in this assembly shares one Aspire-hosted stack
// (ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)) -
// Postgres, Keycloak, the backend API, MinIO, Mailpit - all on the same
// runner. Most DB-touching classes carry [NotInParallel("IntegrationDb")],
// which only serializes tests *within* that key - it does nothing to cap how
// many classes (or that group as a whole) run at once, so with no
// assembly-wide limit, TUnit's default unbounded concurrency can pile enough
// concurrent HTTP calls onto the shared stack to starve Postgres of CPU,
// occasionally tipping a raw connection read over its timeout.
//
// Same root cause as VisualTests/AssemblyParallelLimit.cs (one shared Aspire
// stack, no assembly-wide cap) - reuses that file's ProcessorCount - 2 limit,
// since VisualTests already found ProcessorCount - 1 insufficient.
[assembly: ParallelLimiter<IntegrationTestsParallelLimit>]

public sealed class IntegrationTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 2);
}
