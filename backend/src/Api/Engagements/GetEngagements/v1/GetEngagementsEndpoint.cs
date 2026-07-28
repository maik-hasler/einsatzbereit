using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Engagements;
using Application.Engagements.GetEngagements.v1;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/volunteer-opportunities/{opportunityId:guid}/engagements", GetEngagementsAsync)
			.WithName("GetEngagements")
			.WithTags("Engagements")
			.Produces<PagedList<EngagementSummary>>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetEngagementsAsync(
		[FromRoute] Guid opportunityId,
		[FromQuery] int pageNumber,
		[FromQuery] int pageSize,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		if (pageNumber < 1)
			return Results.Problem("PageNumber must be at least 1.", statusCode: StatusCodes.Status400BadRequest);

		if (pageSize < 1 || pageSize > 100)
			return Results.Problem("PageSize must be between 1 and 100.", statusCode: StatusCodes.Status400BadRequest);

		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? UserId.Create(uid).GetValueOrThrow() : throw new ResultFailureException(Error.Validation("User.InvalidId", "Invalid user."));
		var query = new GetEngagementsQuery(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), userId, pageNumber, pageSize);
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
