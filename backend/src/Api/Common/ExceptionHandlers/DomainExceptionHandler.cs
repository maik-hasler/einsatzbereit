using Domain.Primitives;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Common.ExceptionHandlers;

internal sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is not DomainException domainException)
			return false;

		var isNotFound = domainException.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
		var statusCode = isNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;

		logger.LogInformation(
			"DomainException handled: {Message} -> {StatusCode}",
			domainException.Message,
			statusCode);

		var problem = new ProblemDetails
		{
			Title = isNotFound ? "Not Found" : "Bad Request",
			Status = statusCode,
			Detail = domainException.Message,
		};

		httpContext.Response.StatusCode = statusCode;
		await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

		return true;
	}
}
