using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Common.ExceptionHandlers;

internal sealed class UnhandledExceptionHandler(
	ILogger<UnhandledExceptionHandler> logger,
	IHostEnvironment environment)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

		var problem = new ProblemDetails
		{
			Title = "Internal Server Error",
			Status = StatusCodes.Status500InternalServerError,
			Detail = environment.IsDevelopment()
				? exception.Message
				: "An unexpected error occurred. Please try again later.",
		};

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

		return true;
	}
}
