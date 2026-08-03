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

				bool shouldUpdate;
				lock (_lock)
				{
					shouldUpdate = !cache.TryGetValue(cacheKey, out _);
					if (shouldUpdate)
					{
						cache.Set(cacheKey, true, NextServerMidnight());
					}
				}

				if (shouldUpdate)
				{
					try
					{
						await sender.Send(
							new RecordLoginCommand(UserId.Create(userId).GetValueOrThrow(), today),
							context.RequestAborted);
					}
					catch
					{
						// never fail a request due to streak tracking
					}
				}
			}
		}

		await next(context);
	}

	private DateTimeOffset NextServerMidnight()
	{
		var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ServerTimeZone);
		var nextMidnight = serverNow.Date.AddDays(1);

		return new DateTimeOffset(nextMidnight, ServerTimeZone.GetUtcOffset(nextMidnight));
	}
}
