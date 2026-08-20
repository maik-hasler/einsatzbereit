using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using Domain.Users;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Common.Middleware;

internal sealed class LoginStreakMiddleware(RequestDelegate next, IMemoryCache cache, TimeProvider timeProvider)
{
	// Fixed anchor for the shared dedup cache below - deliberately NOT derived from
	// the request's X-Timezone header. The header is attacker-controlled and used
	// to previously reset a single process-wide "today" variable, so alternating
	// timezones across requests could thrash it and force RecordLoginCommand (a DB
	// write) to refire for every user on every request (#1185).
	private static readonly TimeZoneInfo ServerTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
	private static readonly Lock _lock = new();

	private static TimeZoneInfo ResolveTimeZone(string? ianaId)
	{
		if (string.IsNullOrWhiteSpace(ianaId))
			return ServerTimeZone;
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
			return ServerTimeZone;
		}
	}

	public async Task InvokeAsync(HttpContext context, ISender sender)
	{
		if (context.User.Identity?.IsAuthenticated == true)
		{
			var subClaim = context.User.FindFirst("sub")?.Value;
			if (subClaim is not null && Guid.TryParse(subClaim, out var userId))
			{
				// X-Timezone only shapes which calendar day this login counts toward
				// for the user's streak - it never influences the cache entry below.
				var tzHeader = context.Request.Headers["X-Timezone"].FirstOrDefault();
				var tz = ResolveTimeZone(tzHeader);
				var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz).DateTime);

				// Per-user cache entry (not a single shared collection) expiring at
				// the next server-timezone midnight, so a user who never logs in
				// again does not sit in memory forever, and no request input can
				// force an early reset.
				var cacheKey = $"login-streak-recorded:{subClaim}";

				// Cache the in-flight write itself, not just a "handled" flag. A page
				// load fires several authenticated requests concurrently (profile,
				// streaks, achievements...), all racing into this middleware for the
				// same user. Storing a bare `true` synchronously and awaiting the DB
				// write only on the winner's own path let every other concurrent
				// request see the flag already set and fall straight through to its
				// own handler - which could read the UserStreak row before the
				// winner's write had actually committed, e.g. GET /v1/me/streaks
				// intermittently returning yesterday's count. Single-flighting the
				// Task means every concurrent request - winner and followers alike -
				// awaits the *same* write below before proceeding.
				Task recordTask;
				lock (_lock)
				{
					if (!cache.TryGetValue(cacheKey, out Task? existing) || existing is null)
					{
						recordTask = RecordLoginSafeAsync(sender, userId, today);
						cache.Set(cacheKey, recordTask, NextServerMidnight());
					}
					else
					{
						recordTask = existing;
					}
				}

				await recordTask;
			}
		}

		await next(context);
	}

	// CancellationToken.None, deliberately not context.RequestAborted: this task
	// is cached and shared across every concurrent request for this user today
	// (see InvokeAsync above), so it must not be cancelled just because the one
	// request whose pipeline happened to start it gets aborted while a sibling
	// request elsewhere is still awaiting this same shared task.
	private static async Task RecordLoginSafeAsync(ISender sender, Guid userId, DateOnly today)
	{
		try
		{
			await sender.Send(
				new RecordLoginCommand(UserId.Create(userId).GetValueOrThrow(), today),
				CancellationToken.None);
		}
		catch
		{
			// never fail a request due to streak tracking
		}
	}

	private DateTimeOffset NextServerMidnight()
	{
		var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ServerTimeZone);
		var nextMidnight = serverNow.Date.AddDays(1);

		return new DateTimeOffset(nextMidnight, ServerTimeZone.GetUtcOffset(nextMidnight));
	}
}
