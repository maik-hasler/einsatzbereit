using AwesomeAssertions;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Infrastructure.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class CheckInAttemptPruneJobTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task PruneExpiredAttemptsAsync_AttemptPastTheLockoutWindow_IsRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (volunteerId, opportunityId) = await SeedAttemptAsync(
			dbContext, lastAttemptOn: now.Add(-CheckInAttemptLimiter.LockoutDuration).AddMinutes(-1), cancellationToken);

		var pruned = await CheckInAttemptPruneJob.PruneExpiredAttemptsAsync(dbContext, now, cancellationToken);

		pruned.Should().Be(1);
		var stillExists = await dbContext.Set<CheckInAttempt>()
			.AsNoTracking()
			.AnyAsync(a => a.VolunteerId == volunteerId && a.OpportunityId == opportunityId, cancellationToken);
		stillExists.Should().BeFalse();
	}

	[Test]
	public async Task PruneExpiredAttemptsAsync_AttemptStillWithinTheLockoutWindow_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var now = DateTimeOffset.UtcNow;
		var (volunteerId, opportunityId) = await SeedAttemptAsync(dbContext, lastAttemptOn: now.AddMinutes(-1), cancellationToken);

		var pruned = await CheckInAttemptPruneJob.PruneExpiredAttemptsAsync(dbContext, now, cancellationToken);

		pruned.Should().Be(0);
		var stillExists = await dbContext.Set<CheckInAttempt>()
			.AsNoTracking()
			.AnyAsync(a => a.VolunteerId == volunteerId && a.OpportunityId == opportunityId, cancellationToken);
		stillExists.Should().BeTrue();
	}

	private static async Task<(Guid VolunteerId, Guid OpportunityId)> SeedAttemptAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset lastAttemptOn,
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var opportunityId = Guid.NewGuid();
		dbContext.Set<CheckInAttempt>().Add(new CheckInAttempt
		{
			VolunteerId = volunteerId,
			OpportunityId = opportunityId,
			FailedAttempts = 1,
			LastAttemptOn = lastAttemptOn,
		});

		await dbContext.SaveChangesAsync(cancellationToken);

		return (volunteerId, opportunityId);
	}
}
