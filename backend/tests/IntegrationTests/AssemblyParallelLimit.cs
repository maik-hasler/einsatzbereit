using TUnit.Core.Interfaces;

// Same root cause VisualTests/AssemblyParallelLimit.cs documents for its own
// project (issue #1339), applying here too: IntegrationTests boots the exact
// same kind of Aspire-hosted stack (Postgres, Keycloak, backend API,
// SharedType.PerTestSession) on the same ubuntu-latest CI runner, but had no
// assembly-wide parallel limit at all - individual test classes use
// [NotInParallel] against each other (e.g. AccountDeletionTests), but that
// only serializes tests sharing the same key, not the ~40 other test classes
// running concurrently against them. TUnit's default lets the runner spin up
// as many concurrent tests as the thread pool allows, all making HTTP calls
// into the one shared backend/Postgres instance - sustained CPU contention on
// a CPU-limited runner starves that stack, which surfaces as the backend or
// Postgres connection failing mid-request (e.g. Npgsql "Exception while
// reading from stream") rather than a clean timeout, since IntegrationTests
// doesn't have VisualTests' own per-test Chromium/axe-core cost to reserve
// against - just one core held back for the stack, not two.
[assembly: ParallelLimiter<IntegrationTestsParallelLimit>]

public sealed class IntegrationTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 1);
}
