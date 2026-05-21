using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.CheckInEngagement.v1;
using Domain.Engagements;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.CheckInEngagement.v1;

internal sealed class CheckInEngagementEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/engagements/{engagementId:guid}/check-in", CheckInAsync)
			.WithName("CheckInEngagement")
			.WithTags("Engagements")
			.Produces<EngagementStatusResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CheckInAsync(
		[FromRoute] Guid engagementId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var command = new CheckInEngagementCommand(new EngagementId(engagementId));
		var engagement = await sender.Send(command, cancellationToken);
		return Results.Ok(new EngagementStatusResponse(engagement.Id.Value, engagement.Status.ToString(), engagement.ModifiedOn));
	}
}
