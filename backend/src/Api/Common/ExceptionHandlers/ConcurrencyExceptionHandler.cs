using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Common.ExceptionHandlers;

internal sealed class ConcurrencyExceptionHandler(
	ILogger<ConcurrencyExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is not DbUpdateConcurrencyException)
			return false;

		var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

		logger.LogInformation(
			exception,
			"Concurrency conflict handled [{TraceId}]: {Message}",
			traceId,
			exception.Message);

		var problem = new ProblemDetails
		{
			Title = "Conflict",
			Status = StatusCodes.Status409Conflict,
			Detail = "This record was changed by someone else in the meantime. Please reload and try again.",
			Extensions = { ["errorCode"] = "Concurrency.Conflict", ["traceId"] = traceId },
		};

		httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
		await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

		return true;
	}
}
