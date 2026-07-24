using System.Collections.Concurrent;
using Application.Common.RateLimiting;
using Domain.Engagements;

namespace Infrastructure.RateLimiting;

// In-memory per-engagement PIN attempt tracking, independent of the generic
// per-user/IP rate limiting policies in Api/Common/RateLimiting. A 4-digit PIN
// has only 10,000 combinations, so a much tighter, engagement-scoped lockout is
// needed to make brute-forcing infeasible even for an authenticated owner.
internal sealed class CheckInAttemptLimiter : ICheckInAttemptLimiter
{
	private const int MaxFailedAttempts = 5;
	private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

	private readonly ConcurrentDictionary<Guid, AttemptState> _attempts = new();

	public Task<bool> IsLockedOutAsync(EngagementId engagementId, CancellationToken cancellationToken = default)
	{
		var isLockedOut = _attempts.TryGetValue(engagementId.Value, out var state)
			&& state.LockedUntil is { } lockedUntil
			&& lockedUntil > DateTimeOffset.UtcNow;

		return Task.FromResult(isLockedOut);
	}

	public Task RegisterFailedAttemptAsync(EngagementId engagementId, CancellationToken cancellationToken = default)
	{
		_attempts.AddOrUpdate(
			engagementId.Value,
			_ => new AttemptState(1, null),
			(_, existing) =>
			{
				var failedAttempts = existing.FailedAttempts + 1;
				var lockedUntil = failedAttempts >= MaxFailedAttempts
					? DateTimeOffset.UtcNow.Add(LockoutDuration)
					: existing.LockedUntil;

				return new AttemptState(failedAttempts, lockedUntil);
			});

		return Task.CompletedTask;
	}

	public Task ResetAsync(EngagementId engagementId, CancellationToken cancellationToken = default)
	{
		_attempts.TryRemove(engagementId.Value, out _);

		return Task.CompletedTask;
	}

	private sealed record AttemptState(int FailedAttempts, DateTimeOffset? LockedUntil);
}
