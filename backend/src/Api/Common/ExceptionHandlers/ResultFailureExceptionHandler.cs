using System.Diagnostics;
using Application.Common.Exceptions;
using Domain.Primitives;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Common.ExceptionHandlers;

internal sealed class ResultFailureExceptionHandler(ILogger<ResultFailureExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is not ResultFailureException resultException)
			return false;

		var error = resultException.Error;

		var statusCode = error.Type switch
		{
			ErrorType.Validation => StatusCodes.Status400BadRequest,
			ErrorType.NotFound => StatusCodes.Status404NotFound,
			ErrorType.Conflict => StatusCodes.Status409Conflict,
			ErrorType.Forbidden => StatusCodes.Status403Forbidden,
			_ => StatusCodes.Status400BadRequest,
		};

		var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

		logger.LogInformation(
			"Result failure handled: {ErrorCode} {Description} -> {StatusCode} (traceId={TraceId})",
			error.Code,
			error.Description,
			statusCode,
			traceId);

		var problem = new ProblemDetails
		{
			Title = error.Type.ToString(),
			Status = statusCode,
			Detail = error.Description,
			Extensions = { ["errorCode"] = error.Code, ["traceId"] = traceId },
		};

		httpContext.Response.StatusCode = statusCode;
		await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

		return true;
	}
}
