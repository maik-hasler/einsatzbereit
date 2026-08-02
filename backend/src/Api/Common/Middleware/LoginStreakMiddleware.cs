using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using Domain.Users;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Common.Middleware;

internal sealed class LoginStreakMiddleware(RequestDelegate next)
{
	// Sliding-expiry entry per (user, their own resolved date) instead of one
	// process-wide date (#1143): the previous static HashSet/DateOnly pair was
	// shared by every user, so two users whose local dates differed (or either
	// side of local midnight) wiped the whole dedupe set on alternate requests,
	// turning every authenticated request into a DB round trip. 48h comfortably
	// outlives the "today" it dedupes without needing to be cleared manually.
	private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(48);

	private static TimeZoneInfo ResolveTimeZone(string? ianaId)
	{
		if (string.IsNullOrWhiteSpace(ianaId))
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		}
	}

	public async Task InvokeAsync(HttpContext context, ISender sender, IMemoryCache cache)
	{
		if (context.User.Identity?.IsAuthenticated == true)
		{
			var subClaim = context.User.FindFirst("sub")?.Value;
			if (subClaim is not null && Guid.TryParse(subClaim, out var userId))
			{
				var tzHeader = context.Request.Headers["X-Timezone"].FirstOrDefault();
				var tz = ResolveTimeZone(tzHeader);
				var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);

				var cacheKey = $"login-streak:{subClaim}:{today:O}";
				if (!cache.TryGetValue(cacheKey, out _))
				{
					cache.Set(cacheKey, true, DedupeWindow);

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
}
