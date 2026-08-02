using AwesomeAssertions;
using Domain.Engagements;
using Infrastructure.RateLimiting;

namespace IntegrationTests;

// Pure in-memory logic - no database needed - but CheckInAttemptLimiter is
// internal to Infrastructure (InternalsVisibleTo only grants IntegrationTests,
// see Infrastructure.csproj), so this has to live here rather than in a
// project that can't see it.
public class CheckInAttemptLimiterTests
{
	[Test]
	public async Task IsLockedOutAsync_ShouldReturnFalse_ForEngagementWithNoAttempts(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter(new FakeTimeProvider());

		var isLockedOut = await sut.IsLockedOutAsync(EngagementId.New(), cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnFalse_WhenFailedAttemptsAreBelowMax(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter(new FakeTimeProvider());
		var engagementId = EngagementId.New();

		for (var i = 0; i < 4; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		var isLockedOut = await sut.IsLockedOutAsync(engagementId, cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnTrue_WhenFailedAttemptsReachMax(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter(new FakeTimeProvider());
		var engagementId = EngagementId.New();

		for (var i = 0; i < 5; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		var isLockedOut = await sut.IsLockedOutAsync(engagementId, cancellationToken);

		isLockedOut.Should().BeTrue();
	}

	[Test]
	public async Task ResetAsync_ShouldClearLockout(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter(new FakeTimeProvider());
		var engagementId = EngagementId.New();

		for (var i = 0; i < 5; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		await sut.ResetAsync(engagementId, cancellationToken);

		var isLockedOut = await sut.IsLockedOutAsync(engagementId, cancellationToken);
		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_ShouldForgetStaleAttempts_OnceLockoutDurationElapses(
		CancellationToken cancellationToken)
	{
		// Regression for #1185: an engagement that never succeeds (ResetAsync only
		// runs on a correct PIN) must not keep its failed-attempt count forever -
		// otherwise the underlying dictionary grows without bound for the
		// container's lifetime. Four failed attempts is below the five-attempt
		// lockout threshold, so if the stale entry is never evicted, a single
		// additional attempt after LockoutDuration has passed would still push the
		// (never-reset) count to five and lock the engagement out immediately.
		var timeProvider = new FakeTimeProvider();
		var sut = new CheckInAttemptLimiter(timeProvider);
		var staleEngagementId = EngagementId.New();
		var otherEngagementId = EngagementId.New();

		for (var i = 0; i < 4; i++)
			await sut.RegisterFailedAttemptAsync(staleEngagementId, cancellationToken);

		timeProvider.Advance(TimeSpan.FromMinutes(16));

		// Any write sweeps stale entries - use an unrelated engagement so this
		// doesn't itself contribute to staleEngagementId's attempt count.
		await sut.RegisterFailedAttemptAsync(otherEngagementId, cancellationToken);

		await sut.RegisterFailedAttemptAsync(staleEngagementId, cancellationToken);

		var isLockedOut = await sut.IsLockedOutAsync(staleEngagementId, cancellationToken);
		isLockedOut.Should().BeFalse("the stale 4-attempt count should have been evicted, leaving only this one fresh attempt");
	}

	private sealed class FakeTimeProvider : TimeProvider
	{
		private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

		public override DateTimeOffset GetUtcNow() => _utcNow;

		public void Advance(TimeSpan by) => _utcNow += by;
	}
}
