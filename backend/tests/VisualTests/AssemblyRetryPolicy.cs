// Regression for #623: ~61 VisualTests classes share one Aspire-hosted stack
// per test session (SharedType.PerTestSession). Under a contended CI runner,
// individual tests intermittently time out waiting on UI state that is slow
// to settle (dialog close, save-then-navigate reads) rather than failing due
// to an actual app defect - a different test flakes this way each release,
// which has repeatedly blocked Deploy to Staging on a transient failure.
// Retrying a failed test re-runs only that test method against the same
// shared stack; a genuine regression still fails on every attempt and still
// fails the suite, but a one-off timing flake gets a chance to pass.
[assembly: Retry(2)]
