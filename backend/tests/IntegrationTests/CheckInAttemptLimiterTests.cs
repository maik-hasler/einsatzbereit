using AwesomeAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Infrastructure.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

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

		(await CheckInAttemptLimiter.IsLockedOutAsync(dbContext, engagementId, afterLockoutWindow, cancellationToken))
			.Should().BeFalse();
	}

	[Test]
	public async Task RegisterFailedAttemptAsync_PersistsAcrossIndependentDbContexts(
		CancellationToken cancellationToken)
	{
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
