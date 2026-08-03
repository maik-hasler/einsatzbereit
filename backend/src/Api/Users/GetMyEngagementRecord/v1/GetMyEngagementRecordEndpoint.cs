using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Engagements;
using Application.Engagements.GetMyEngagementRecord.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.GetMyEngagementRecord.v1;

internal sealed class GetMyEngagementRecordEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/me/engagement-record", GetMyEngagementRecordAsync)
			.WithName("GetMyEngagementRecord")
			.WithTags("Users")
			.Produces<List<EngagementRecordEntry>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetMyEngagementRecordAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem(
				"Unable to identify the current user.",
				statusCode: StatusCodes.Status401Unauthorized);
		}

		var query = new GetMyEngagementRecordQuery(UserId.Create(userId).GetValueOrThrow());
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
