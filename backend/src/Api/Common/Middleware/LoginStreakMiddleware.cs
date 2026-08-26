using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using Domain.Users;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Common.Middleware;

internal sealed class LoginStreakMiddleware(RequestDelegate next, IMemoryCache cache, TimeProvider timeProvider)
{
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
				var tzHeader = context.Request.Headers["X-Timezone"].FirstOrDefault();
				var tz = ResolveTimeZone(tzHeader);
				var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz).DateTime);

				var cacheKey = $"login-streak-recorded:{subClaim}";

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
							AbsoluteExpiration = NextServerMidnight(),
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

	private DateTimeOffset NextServerMidnight()
	{
		var serverNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ServerTimeZone);
		var nextMidnight = serverNow.Date.AddDays(1);

		return new DateTimeOffset(nextMidnight, ServerTimeZone.GetUtcOffset(nextMidnight));
	}
}
