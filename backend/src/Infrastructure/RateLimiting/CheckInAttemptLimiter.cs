using Application.Common.RateLimiting;
using Domain.Engagements;
using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.RateLimiting;

internal sealed class CheckInAttemptLimiter(
	IServiceScopeFactory scopeFactory)
	: ICheckInAttemptLimiter
{
	internal const int MaxFailedAttempts = 5;

	internal static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

	public async Task<bool> IsLockedOutAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		return await IsLockedOutAsync(dbContext, engagementId.Value, DateTimeOffset.UtcNow, cancellationToken);
	}

	public async Task RegisterFailedAttemptAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		await RegisterFailedAttemptAsync(dbContext, engagementId.Value, DateTimeOffset.UtcNow, cancellationToken);
	}

	public async Task ResetAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		await ResetAsync(dbContext, engagementId.Value, cancellationToken);
	}

	internal static async Task<bool> IsLockedOutAsync(
		ApplicationDbContext dbContext,
		Guid engagementId,
		DateTimeOffset now,
		CancellationToken cancellationToken = default)
	{
		var lockedUntil = await dbContext.Set<CheckInAttempt>()
			.AsNoTracking()
			.Where(a => a.EngagementId == engagementId)
			.Select(a => a.LockedUntil)
			.FirstOrDefaultAsync(cancellationToken);

		return lockedUntil is { } value && value > now;
	}

	internal static async Task RegisterFailedAttemptAsync(
		ApplicationDbContext dbContext,
		Guid engagementId,
		DateTimeOffset now,
		CancellationToken cancellationToken = default)
	{
		var attempt = await dbContext.Set<CheckInAttempt>()
			.FirstOrDefaultAsync(a => a.EngagementId == engagementId, cancellationToken);

		var isNew = attempt is null;
		if (attempt is null)
		{
			attempt = new CheckInAttempt { EngagementId = engagementId };
			dbContext.Set<CheckInAttempt>().Add(attempt);
		}

		ApplyFailedAttempt(attempt, now);

		if (!isNew)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
			return;
		}

		try
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			dbContext.Entry(attempt).State = EntityState.Detached;

			attempt = await dbContext.Set<CheckInAttempt>()
				.SingleAsync(a => a.EngagementId == engagementId, cancellationToken);

			ApplyFailedAttempt(attempt, now);

			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	// IsLockedOutAsync is always checked (and short-circuits) before this method is
	// ever called, so a persisted LockedUntil reaching here has already expired -
	// without this, FailedAttempts only ever grew, so once it first hit
	// MaxFailedAttempts the very next wrong guess re-locked for another full
	// LockoutDuration forever, with no way to ever earn a fresh attempt budget
	// again (#1159, ported from the earlier in-memory limiter this replaced).
	private static void ApplyFailedAttempt(CheckInAttempt attempt, DateTimeOffset now)
	{
		var previousLockoutExpired = attempt.LockedUntil is not null;
		attempt.FailedAttempts = previousLockoutExpired ? 1 : attempt.FailedAttempts + 1;
		attempt.LastAttemptOn = now;
		attempt.LockedUntil = attempt.FailedAttempts >= MaxFailedAttempts
			? now.Add(LockoutDuration)
			: null;
	}

	internal static async Task ResetAsync(
		ApplicationDbContext dbContext,
		Guid engagementId,
		CancellationToken cancellationToken = default)
	{
		// A tracked fetch-then-Remove, not ExecuteDeleteAsync: ExecuteDelete
		// issues a raw DELETE that bypasses the change tracker entirely, which
		// would leave a CheckInAttempt instance a caller already tracked on this
		// same DbContext (e.g. from a prior RegisterFailedAttemptAsync call)
		// stale - believing the now-deleted row still exists - and throw an
		// "already tracked" conflict the next time this engagement's row is
		// looked up on that DbContext.
		var attempt = await dbContext.Set<CheckInAttempt>()
			.FirstOrDefaultAsync(a => a.EngagementId == engagementId, cancellationToken);

		if (attempt is null)
			return;

		dbContext.Set<CheckInAttempt>().Remove(attempt);
		await dbContext.SaveChangesAsync(cancellationToken);
	}
}
