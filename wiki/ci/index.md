# CI failures

Recurring CI failure causes and the CI-enforced conventions behind them.

- [CI bans Unicode dashes](ci-bans-unicode-dashes.md) - a repo-wide grep for em/en dash characters
- [Indentation is tabs by default](indentation-tabs-default.md) - only JSON, YAML, and Markdown override to spaces
- [Aspire.Hosting.Testing http/https flip](aspire-testing-http-https-flip.md) - a dependency bump changed a default endpoint scheme
- [ArchitectureTests enforces Clean Architecture](architecture-tests-clean-architecture.md) - layering, naming, and rate-limiting as CI-blocking tests
- [VisualTests flakiness handled with Retry(2)](visualtests-flaky-retry.md) - a bounded per-test retry, not a loosened gate
