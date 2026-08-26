using System.Diagnostics;
using Microsoft.AspNetCore.HttpLogging;

namespace Api.Common.Logging;

// UseHttpLogging() runs ahead of the TraceId/UserId BeginScope block in Program.cs (the
// scope needs authentication to have already populated ctx.User first), so the request
// summary line it emits for every ordinary request carries neither today - only a
// downstream log statement inside that scope does. This interceptor is the supported
// way to stamp a field onto that same request/response log entry instead of reordering
// a pipeline the rest of Program.cs already depends on staying in its current order.
internal sealed class TraceIdHttpLoggingInterceptor : IHttpLoggingInterceptor
{
	public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
	{
		logContext.AddParameter(
			"TraceId",
			Activity.Current?.TraceId.ToString() ?? logContext.HttpContext.TraceIdentifier);

		return ValueTask.CompletedTask;
	}

	public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext) => ValueTask.CompletedTask;
}
