using Application.Common.RateLimiting;
using Domain.Engagements;
using Infrastructure.Persistence;
using Infrastructure.Persistence.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.RateLimiting;

// Persisted per-engagement PIN attempt tracking (#1176), independent of the
// generic per-user/IP rate limiting policies in Api/Common/RateLimiting. A
// 4-6 digit PIN has only a small combination space, so a much tighter,
// engagement-scoped lockout is needed to make brute-forcing infeasible even
// for an authenticated owner.
//
// Every operation opens its own scope/DbContext (a fresh connection and
// transaction) instead of reusing the caller's ambient, request-scoped one.
// This is deliberate: CheckInWithPinCommandHandler registers a failed attempt
// and then immediately throws to signal the invalid PIN, which rolls back the
// TransactionPipelineBehavior-owned transaction wrapping the whole command -
// an attempt tracked on that same connection would be rolled back right along
// with it, silently disabling the lockout on every wrong guess.
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

	// Exposed so IntegrationTests can exercise the lockout logic directly
	// against a real ApplicationDbContext instead of standing up the full DI
	// container just to obtain an IServiceScopeFactory.
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

		attempt.FailedAttempts++;
		attempt.LastAttemptOn = now;
		if (attempt.FailedAttempts >= MaxFailedAttempts)
			attempt.LockedUntil = now.Add(LockoutDuration);

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
			// Two concurrent wrong guesses for the same engagement (a double-click,
			// or an attacker racing the lockout itself) can both find no existing
			// row and race to insert one - the loser hits a primary key violation
			// here. Detach the failed insert and fall through to a plain update
			// against the row the winner just created, instead of losing this
			// attempt or surfacing an unhandled 500.
			dbContext.Entry(attempt).State = EntityState.Detached;

			attempt = await dbContext.Set<CheckInAttempt>()
				.SingleAsync(a => a.EngagementId == engagementId, cancellationToken);

			attempt.FailedAttempts++;
			attempt.LastAttemptOn = now;
			if (attempt.FailedAttempts >= MaxFailedAttempts)
				attempt.LockedUntil = now.Add(LockoutDuration);

			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	internal static async Task ResetAsync(
		ApplicationDbContext dbContext,
		Guid engagementId,
		CancellationToken cancellationToken = default) =>
		await dbContext.Set<CheckInAttempt>()
			.Where(a => a.EngagementId == engagementId)
			.ExecuteDeleteAsync(cancellationToken);
}
