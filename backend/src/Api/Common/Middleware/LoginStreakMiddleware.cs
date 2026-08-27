using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Time;
using Application.Users.RecordLogin.v1;
using Domain.Users;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Common.Middleware;

internal sealed class LoginStreakMiddleware(RequestDelegate next, IMemoryCache cache, TimeProvider timeProvider)
{
	private static readonly Lock _lock = new();

	public async Task InvokeAsync(HttpContext context, ISender sender)
	{
		if (context.User.Identity?.IsAuthenticated == true)
		{
			var subClaim = context.User.FindFirst("sub")?.Value;
			if (subClaim is not null && Guid.TryParse(subClaim, out var userId))
			{
				var tzHeader = context.Request.Headers["X-Timezone"].FirstOrDefault();
				var tz = CanonicalTimeZone.Resolve(tzHeader);
				var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz).DateTime);

				// The date is part of the key (not just the expiration) so that a client
				// whose local day advances before the next cache rollover - e.g. a caller
				// far enough ahead of tz's own midnight - still gets recorded for its new
				// local day instead of being swallowed by yesterday's cached entry (#2203).
				var cacheKey = $"login-streak-recorded:{subClaim}:{today:O}";

				Task recordTask;
				lock (_lock)
				{
					if (!cache.TryGetValue(cacheKey, out Task? existing) || existing is null)
					{
						recordTask = RecordLoginSafeAsync(sender, userId, today);
						// Nominal Size - the shared cache's SizeLimit budget is denominated in the
						// tile bytes OpenStreetMapTileService caches, which dwarf this payload (#2215).
						cache.Set(cacheKey, recordTask, new MemoryCacheEntryOptions
						{
							Size = 1,
							AbsoluteExpiration = NextMidnight(today, tz),
						});
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

	private static DateTimeOffset NextMidnight(DateOnly today, TimeZoneInfo tz)
	{
		var nextMidnight = today.AddDays(1).ToDateTime(TimeOnly.MinValue);

		return new DateTimeOffset(nextMidnight, tz.GetUtcOffset(nextMidnight));
	}
}
