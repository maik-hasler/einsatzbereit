using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Common.RateLimiting;

internal static class RateLimitingExtensions
{
	public static IServiceCollection AddRateLimitingPolicies(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var options = configuration
			.GetSection("RateLimiting")
			.Get<RateLimitingOptions>() ?? new RateLimitingOptions();

		return services.AddRateLimiter(limiter =>
		{
			limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

			// Lets a well-behaved client (and #2208's client-side backoff) know
			// exactly how long to wait instead of guessing and retrying blind,
			// which would only deepen the limit.
			limiter.OnRejected = (context, _) =>
			{
				if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
				{
					context.HttpContext.Response.Headers.RetryAfter =
						((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
				}

				return ValueTask.CompletedTask;
			};

			var readWindow = TimeSpan.FromSeconds(options.Read.WindowSeconds);
			var writeWindow = TimeSpan.FromSeconds(options.Write.WindowSeconds);
			var mapTilesWindow = TimeSpan.FromSeconds(options.MapTiles.WindowSeconds);

			limiter.AddPolicy(RateLimitingPolicies.Read, httpContext =>
			{
				if (httpContext.User.Identity?.IsAuthenticated == true)
				{
					var userId = httpContext.User.FindFirstValue("sub") ?? "unknown";
					return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = options.Read.AuthenticatedPermitLimit,
						Window = readWindow,
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					});
				}

				var clientIp = GetClientIp(httpContext);
				return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = options.Read.AnonymousPermitLimit,
					Window = readWindow,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
					QueueLimit = 0
				});
			});

			limiter.AddPolicy(RateLimitingPolicies.Write, httpContext =>
			{
				var key = httpContext.User.FindFirstValue("sub") ?? GetClientIp(httpContext);
				return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = options.Write.PermitLimit,
					Window = writeWindow,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
					QueueLimit = 0
				});
			});

			// Always keyed by IP, never by user: tiles are drawn by Leaflet as
			// plain <img> tags, which never carry the Authorization header the
			// API client attaches to fetch() calls, so every tile request looks
			// anonymous to the backend regardless of whether the visitor is
			// signed in.
			limiter.AddPolicy(RateLimitingPolicies.MapTiles, httpContext =>
			{
				var clientIp = GetClientIp(httpContext);
				return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = options.MapTiles.PermitLimit,
					Window = mapTilesWindow,
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
					QueueLimit = 0
				});
			});
		});
	}

	// X-Forwarded-For is no longer read directly here (#1332): ForwardedHeadersMiddleware
	// (see Program.cs + TrustedNetworksOptions) already rewrote Connection.RemoteIpAddress
	// from it, but only when the immediate connection came from a known trusted network -
	// otherwise the header is a client-controlled value with no verification at all, and
	// trusting it here would let any caller bypass the anonymous rate limit by sending a
	// different one per request.
	internal static string GetClientIp(HttpContext ctx) =>
		ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
