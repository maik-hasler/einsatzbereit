using System.Diagnostics;

namespace Api.Common.Middleware;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
	{
		var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
		var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";

		using (logger.BeginScope(new Dictionary<string, object>
		{
			["TraceId"] = traceId,
			["UserId"] = userId,
		}))
		{
			await next(context);
		}
	}
}
