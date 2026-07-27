using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Reports.CreateReport.v1;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Reports.CreateReport.v1;

internal sealed class CreateReportEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/reports", CreateReportAsync)
			.WithName("CreateReport")
			.Produces<CreateReportResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CreateReportAsync(
		[FromBody] CreateReportRequest request,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var reporterId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? UserId.Create(uid).GetValueOrThrow()
			: throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));

		if (!Enum.TryParse<ReportedContentType>(request.ContentType, ignoreCase: true, out var contentType))
			return Results.Problem(
				"Invalid content type. Allowed values: VolunteerOpportunity, Organization.",
				statusCode: StatusCodes.Status400BadRequest);

		if (!Enum.TryParse<ReportReason>(request.Reason, ignoreCase: true, out var reason))
			return Results.Problem(
				"Invalid reason. Allowed values: Spam, IllegalContent, Fraud, Other.",
				statusCode: StatusCodes.Status400BadRequest);

		var command = new CreateReportCommand(request.ContentId, contentType, reporterId, reason, request.Detail);

		var id = await sender.Send(command, cancellationToken);

		return Results.Ok(new CreateReportResponse(id));
	}
}
