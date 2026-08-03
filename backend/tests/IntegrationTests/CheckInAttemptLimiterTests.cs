using AwesomeAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Infrastructure.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Exercises Infrastructure.RateLimiting.CheckInAttemptLimiter's static core
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real
// integration Postgres - the persisted replacement for the old in-memory
// ConcurrentDictionary-backed lockout (#1176). CheckInWithPinCommandHandlerTests
// (Application.UnitTests) covers the handler's orchestration against a mocked
// ICheckInAttemptLimiter; this covers the lockout arithmetic and persistence
// itself.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class CheckInAttemptLimiterTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task IsLockedOutAsync_ShouldReturnFalse_ForEngagementWithNoAttempts(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;

		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, Guid.NewGuid(), now, cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_FewerThanMaxAttempts_DoesNotLockOut(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts - 1; i++)
			await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);

		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, now, cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_ReachingMaxAttempts_LocksOut(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts; i++)
			await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);

		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, now, cancellationToken);

		isLockedOut.Should().BeTrue();
	}

	[Test]
	public async Task IsLockedOutAsync_AfterLockoutWindowElapses_IsNoLongerLockedOut(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts; i++)
			await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);

		var afterLockoutWindow = now.Add(CheckInAttemptLimiter.LockoutDuration).AddSeconds(1);
		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, afterLockoutWindow, cancellationToken);

		isLockedOut.Should().BeFalse();
	}

	[Test]
	public async Task ResetAsync_ClearsFailedAttempts_SoSubsequentAttemptsStartFresh(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts; i++)
			await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);

		await CheckInAttemptLimiter.ResetAsync(dbContext, engagementId, cancellationToken);
		await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);

		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, now, cancellationToken);

		isLockedOut.Should().BeFalse("Reset must clear the prior failure count, not just the lock");
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_ShouldStartAFreshAttemptBudget_OnceAPreviousLockoutHasExpired(
		CancellationToken cancellationToken)
	{
		// Regression for #1159: FailedAttempts only ever grew, so once it first hit
		// MaxFailedAttempts the very next wrong guess re-locked for another full
		// LockoutDuration forever, with no way to ever earn a fresh attempt budget -
		// even long after the original lockout had actually expired. This bug was
		// originally fixed in the in-memory ConcurrentDictionary-backed limiter this
		// class replaced (#1176), and had to be re-ported here since the DB-persisted
		// rewrite reintroduced it independently.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts; i++)
			await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, now, cancellationToken);
		(await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, now, cancellationToken))
			.Should().BeTrue("5 failures trip the lockout");

		var afterLockoutWindow = now.Add(CheckInAttemptLimiter.LockoutDuration).AddSeconds(1);
		(await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, afterLockoutWindow, cancellationToken))
			.Should().BeFalse("the lockout has now elapsed");

		await CheckInAttemptLimiter.RegisterFailedAttemptAsync(dbContext, engagementId, afterLockoutWindow, cancellationToken);

		// A single wrong guess after expiry must not immediately re-lock - the old
		// (buggy) behavior would have jumped straight back to "locked" here, since
		// FailedAttempts had never been reset and was already >= MaxFailedAttempts.
		(await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, afterLockoutWindow, cancellationToken))
			.Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_PersistsAcrossIndependentDbContexts(
		CancellationToken cancellationToken)
	{
		// Regression for #1176: the old ConcurrentDictionary-backed limiter lost
		// all state on a process restart. Using two independent
		// ApplicationDbContexts (separate connections, like CheckInAttemptLimiter's
		// own per-call scope) proves the state actually round-trips through
		// Postgres rather than surviving only in a shared in-process dictionary.
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		await using (var writeContext = fixture.CreateApplicationDbContext())
		{
			for (var i = 0; i < CheckInAttemptLimiter.MaxFailedAttempts; i++)
				await CheckInAttemptLimiter.RegisterFailedAttemptAsync(writeContext, engagementId, now, cancellationToken);
		}

		await using var readContext = fixture.CreateApplicationDbContext();
		var isLockedOut = await CheckInAttemptLimiter.IsLockedOutAsync(readContext, engagementId, now, cancellationToken);

		isLockedOut.Should().BeTrue();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_TwoConcurrentFirstAttemptsForTheSameEngagement_BothCount(
		CancellationToken cancellationToken)
	{
		// Two independent ApplicationDbContexts (separate connections), like two
		// near-simultaneous wrong PIN guesses for the same engagement (a
		// double-click, or an attacker racing the lockout) would each get from
		// CheckInAttemptLimiter's own per-call scope. Both find no existing row
		// and race to insert one; the loser must recover from the primary key
		// conflict and still record its attempt instead of throwing or being
		// silently dropped.
		var engagementId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		await using var contextA = fixture.CreateApplicationDbContext();
		await using var contextB = fixture.CreateApplicationDbContext();

		await Task.WhenAll(
			CheckInAttemptLimiter.RegisterFailedAttemptAsync(contextA, engagementId, now, cancellationToken),
			CheckInAttemptLimiter.RegisterFailedAttemptAsync(contextB, engagementId, now, cancellationToken));

		await using var readContext = fixture.CreateApplicationDbContext();
		var failedAttempts = await readContext.Set<CheckInAttempt>()
			.AsNoTracking()
			.Where(a => a.EngagementId == engagementId)
			.Select(a => a.FailedAttempts)
			.SingleAsync(cancellationToken);

		failedAttempts.Should().Be(2, "neither concurrent attempt should be lost to the insert race");
	}
}
