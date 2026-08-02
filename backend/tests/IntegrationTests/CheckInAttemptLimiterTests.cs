using System.Collections;
using System.Reflection;
using AwesomeAssertions;
using Domain.Engagements;
using Infrastructure.RateLimiting;

namespace IntegrationTests;

// Exercises Infrastructure.RateLimiting.CheckInAttemptLimiter directly (InternalsVisibleTo,
// see Infrastructure.csproj) - a pure in-memory class with no external dependency, so unlike
// the rest of this project's tests this needs no Postgres/Keycloak/Aspire fixture at all.
public class CheckInAttemptLimiterTests
{
	private static EngagementId AnyEngagementId() => EngagementId.New();

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnFalse_ForAnEngagementWithNoAttemptsYet(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter();

		var isLockedOut = await sut.IsLockedOutAsync(AnyEngagementId(), cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnTrue_AfterFiveFailedAttempts(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter();
		var engagementId = AnyEngagementId();

		for (var i = 0; i < 5; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeTrue();
	}

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnFalse_AfterFourFailedAttempts(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter();
		var engagementId = AnyEngagementId();

		for (var i = 0; i < 4; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeFalse();
	}

	[Test]
	public async Task ResetAsync_ShouldClearFailedAttempts_SoANewLockoutNeedsFiveMoreFailures(
		CancellationToken cancellationToken)
	{
		var sut = new CheckInAttemptLimiter();
		var engagementId = AnyEngagementId();
		for (var i = 0; i < 4; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		await sut.ResetAsync(engagementId, cancellationToken);
		await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		// Only 1 failure since the reset - nowhere near the 5-attempt threshold.
		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_ShouldStartAFreshAttemptBudget_OnceAPreviousLockoutHasExpired(
		CancellationToken cancellationToken)
	{
		// Regression for #1159: FailedAttempts only ever grew, so once it first hit
		// MaxFailedAttempts the very next wrong guess re-locked for another full
		// LockoutDuration forever, with no way to ever earn a fresh attempt budget -
		// even long after the original 15-minute lockout had actually expired.
		var sut = new CheckInAttemptLimiter();
		var engagementId = AnyEngagementId();
		for (var i = 0; i < 5; i++)
			await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);
		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeTrue("5 failures trip the lockout");

		// The class has no injected clock (a plain in-memory rate limiter, like the
		// rest of Api/Common/RateLimiting), so simulate "15 minutes later" by
		// reflecting the recorded LockedUntil into the past directly rather than
		// waiting out a real 15-minute timer in a test.
		SetLockedUntilInThePast(sut, engagementId);
		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeFalse("the (simulated) lockout has now elapsed");

		await sut.RegisterFailedAttemptAsync(engagementId, cancellationToken);

		// A single wrong guess after expiry must not immediately re-lock - the old
		// (buggy) behavior would have jumped straight back to "locked" here, since
		// FailedAttempts had never been reset and was already >= MaxFailedAttempts.
		(await sut.IsLockedOutAsync(engagementId, cancellationToken)).Should().BeFalse();
	}

	private static void SetLockedUntilInThePast(CheckInAttemptLimiter limiter, EngagementId engagementId)
	{
		var attemptsField = typeof(CheckInAttemptLimiter).GetField("_attempts", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("CheckInAttemptLimiter._attempts field not found - has the implementation changed?");
		var attempts = (IDictionary)attemptsField.GetValue(limiter)!;

		var stateType = typeof(CheckInAttemptLimiter).GetNestedType("AttemptState", BindingFlags.NonPublic)!;
		var expiredState = Activator.CreateInstance(stateType, 5, DateTimeOffset.UtcNow.AddMinutes(-1));

		attempts[engagementId.Value] = expiredState!;
	}
}
